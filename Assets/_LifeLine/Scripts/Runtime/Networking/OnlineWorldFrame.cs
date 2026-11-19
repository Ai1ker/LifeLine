using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace Lifeline
{
    [Serializable]
    public sealed class OnlineWorldFrame
    {
        public bool baseline, hostPaused, waitingForCrew;
        public SessionPhase phase;
        public GameSaveData state;
        public OnlineStationState station;
        public bool winchActive, boarding;
        public float winchFeedbackTime, boardingProgress;
        public string winchFeedback;
        public float[] reviveProgress;
        public bool[] reviving;
        public int helmsman = -1;
    }

    public static class OnlineFrameCodec
    {
        const int TextLimit = 16 * 1024 * 1024;
        public static ArraySegment<byte> Encode(OnlineWorldFrame frame)
        {
            byte[] raw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(frame));
            if (raw.Length > TextLimit) throw new InvalidDataException("Online state exceeds its size limit.");
            using var packet = new MemoryStream();
            using (var zip = new GZipStream(packet, System.IO.Compression.CompressionLevel.Fastest, true)) zip.Write(raw, 0, raw.Length);
            return new ArraySegment<byte>(packet.GetBuffer(), 0, (int)packet.Length);
        }

        public static OnlineWorldFrame Decode(ArraySegment<byte> packet)
        {
            using var stream = new MemoryStream(packet.Array, packet.Offset, packet.Count, false);
            using var zip = new GZipStream(stream, CompressionMode.Decompress);
            using var text = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = zip.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (text.Length + read > TextLimit) throw new InvalidDataException("Online state exceeds its size limit.");
                text.Write(buffer, 0, read);
            }
            return JsonUtility.FromJson<OnlineWorldFrame>(Encoding.UTF8.GetString(text.GetBuffer(), 0, (int)text.Length));
        }
    }
}
