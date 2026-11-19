using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;

namespace Lifeline
{
    public sealed class OnlineRoomMember
    {
        public string SteamId { get; }
        public string DisplayName { get; }
        public CrewRole? Role { get; }
        public bool IsHost { get; }

        public OnlineRoomMember(string steamId, string displayName, CrewRole? role, bool isHost)
        {
            SteamId = steamId;
            DisplayName = displayName;
            Role = role;
            IsHost = isHost;
        }
    }

    /// <summary>Steam lobby state shared by the menu and the online game coordinator.</summary>
    public abstract class OnlineRoomService : MonoBehaviour
    {
        public const int Capacity = 2;
        public const int RoomCodeLength = 6;
        const string CodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        static OnlineRoomService instance;
        public static OnlineRoomService Existing => instance;
        public static Func<OnlineRoomService> Factory { get; set; }
        public static OnlineRoomService Instance
        {
            get
            {
                if (instance == null && Factory != null) instance = Factory();
                return instance;
            }
        }

        public abstract bool Available { get; }
        public abstract bool Busy { get; }
        public abstract bool InRoom { get; }
        public abstract bool IsHost { get; }
        public abstract bool Started { get; }
        public bool CanStart => IsHost && !Busy && !Started && Members.Count == Capacity
            && Members[0].Role.HasValue && Members[1].Role.HasValue && Members[0].Role != Members[1].Role;
        public abstract bool ResumeSavedGame { get; }
        public abstract string RoomCode { get; }
        public abstract string StatusKey { get; }
        public abstract string LocalSteamId { get; }
        public abstract string HostSteamId { get; }
        public abstract string MatchId { get; }
        public abstract IReadOnlyList<OnlineRoomMember> Members { get; }
        public abstract OnlineRoomMember ReservedGuest { get; }

        public event Action Changed;
        protected void NotifyChanged() => Changed?.Invoke();
        public abstract void RefreshAvailability();
        public abstract void Create(bool resumeSavedGame);
        public abstract void Join(string roomCode);
        public abstract void SelectRole(CrewRole role);
        public abstract void StartGame();
        public abstract void Leave();
        public abstract bool TryGetAssignedRole(string steamId, out CrewRole role);

        public static bool TryNormalizeRoomCode(string input, out string code)
        {
            code = (input ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length != RoomCodeLength) return false;
            bool letter = false;
            bool digit = false;
            foreach (char character in code)
            {
                if (character >= 'A' && character <= 'Z') letter = true;
                else if (character >= '0' && character <= '9') digit = true;
                else return false;
            }
            return letter && digit;
        }

        public static string GenerateRoomCode()
        {
            using var random = RandomNumberGenerator.Create();
            var bytes = new byte[RoomCodeLength];
            var characters = new char[RoomCodeLength];
            // Drop the final four possible byte values so all 36 characters have equal odds.
            const int sampleLimit = 252;
            while (true)
            {
                random.GetBytes(bytes);
                int position = 0;
                foreach (byte value in bytes)
                {
                    if (value >= sampleLimit) break;
                    characters[position++] = CodeAlphabet[value % CodeAlphabet.Length];
                }
                if (position == RoomCodeLength && TryNormalizeRoomCode(new string(characters), out string code)) return code;
            }
        }
    }
}
