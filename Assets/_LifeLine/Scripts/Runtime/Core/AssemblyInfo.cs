using System.Runtime.CompilerServices;

// Tests may restore temporary validation state without making that hook part of the gameplay API.
[assembly: InternalsVisibleTo("LifeLine.Tests.EditMode")]
