using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    // Tests must remain compilable when the optional Runtime/Development folder is deleted.
    internal static class DeveloperTestAccess
    {
        public static Component AddTools(GameObject root)
        {
            var type = typeof(GameSession).Assembly.GetType("Lifeline.Development.DeveloperTestTools");
            if (type == null) Assert.Ignore("Optional developer tools were removed.");
            return root.AddComponent(type);
        }

        public static object Invoke(object target, string method, params object[] arguments)
        {
            var member = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (member == null) Assert.Ignore("Optional developer method was removed: " + method);
            return member.Invoke(target, arguments);
        }

        public static bool InfiniteOxygen(GameSession game)
        {
            var field = typeof(GameSession).GetField("developerInfiniteOxygen", BindingFlags.Instance | BindingFlags.Public);
            if (field == null) Assert.Ignore("Optional developer oxygen override was removed.");
            return (bool)field.GetValue(game);
        }
    }
}
