// Minimal NUnit-compatible shim so EditMode tests can run outside Unity. Supports the subset documented in verify.sh.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class TestFixtureAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class TestAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class SetUpAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class TearDownAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)] public sealed class TestCaseAttribute : Attribute { public object[] Args; public TestCaseAttribute(params object[] args) { Args = args; } }
    public delegate void TestDelegate();
    public sealed class AssertionException : Exception { public AssertionException(string m) : base(m) { } }
    public static class Assert
    {
        static void F(string m, string u) => throw new AssertionException(string.IsNullOrEmpty(u) ? m : m + " :: " + u);
        public static void AreEqual(object expected, object actual, string message = null) { if (!Equals(expected, actual) && !NumEq(expected, actual)) F($"Expected {expected} but was {actual}", message); }
        static bool NumEq(object a, object b) { try { return a != null && b != null && IsNum(a) && IsNum(b) && Convert.ToDouble(a) == Convert.ToDouble(b); } catch { return false; } }
        static bool IsNum(object o) => o is int || o is float || o is double || o is long || o is short || o is byte || o is uint || o is sbyte;
        public static void AreEqual(float expected, float actual, float delta, string message = null) { if (Math.Abs(expected - actual) > delta) F($"Expected {expected} ±{delta} but was {actual}", message); }
        public static void AreEqual(double expected, double actual, double delta, string message = null) { if (Math.Abs(expected - actual) > delta) F($"Expected {expected} ±{delta} but was {actual}", message); }
        public static void AreNotEqual(object notExpected, object actual, string message = null) { if (Equals(notExpected, actual)) F($"Did not expect {actual}", message); }
        public static void AreSame(object expected, object actual, string message = null) { if (!ReferenceEquals(expected, actual)) F("Expected same instance", message); }
        public static void IsTrue(bool c, string message = null) { if (!c) F("Expected true", message); }
        public static void IsFalse(bool c, string message = null) { if (c) F("Expected false", message); }
        public static void True(bool c, string message = null) => IsTrue(c, message);
        public static void False(bool c, string message = null) => IsFalse(c, message);
        public static void IsNull(object o, string message = null) { if (o != null) F($"Expected null but was {o}", message); }
        public static void IsNotNull(object o, string message = null) { if (o == null) F("Expected not null", message); }
        public static void NotNull(object o, string message = null) => IsNotNull(o, message);
        public static void Null(object o, string message = null) => IsNull(o, message);
        public static void Greater(double a, double b, string message = null) { if (!(a > b)) F($"Expected {a} > {b}", message); }
        public static void GreaterOrEqual(double a, double b, string message = null) { if (!(a >= b)) F($"Expected {a} >= {b}", message); }
        public static void Less(double a, double b, string message = null) { if (!(a < b)) F($"Expected {a} < {b}", message); }
        public static void LessOrEqual(double a, double b, string message = null) { if (!(a <= b)) F($"Expected {a} <= {b}", message); }
        public static void IsEmpty(System.Collections.IEnumerable e, string message = null) { if (e.Cast<object>().Any()) F("Expected empty", message); }
        public static void IsNotEmpty(System.Collections.IEnumerable e, string message = null) { if (!e.Cast<object>().Any()) F("Expected not empty", message); }
        public static void Contains(object expected, System.Collections.ICollection actual, string message = null) { if (!actual.Cast<object>().Contains(expected)) F($"Expected collection to contain {expected}", message); }
        public static T Throws<T>(TestDelegate code, string message = null) where T : Exception { try { code(); } catch (T e) { return e; } catch (Exception e) { F($"Expected {typeof(T).Name} but got {e.GetType().Name}", message); } F($"Expected {typeof(T).Name}", message); return null; }
        public static void DoesNotThrow(TestDelegate code, string message = null) { try { code(); } catch (Exception e) { F($"Unexpected {e.GetType().Name}: {e.Message}", message); } }
        public static void Fail(string message = null) => F("Fail", message);
        public static void Pass(string message = null) { }
    }
}
namespace TestShimRunner
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            int pass = 0, fail = 0;
            var filter = args.Length > 0 ? args[0] : null;
            foreach (var t in typeof(Program).Assembly.GetTypes().Where(t => t.GetCustomAttribute<NUnit.Framework.TestFixtureAttribute>() != null || t.GetMethods().Any(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null || m.GetCustomAttributes<NUnit.Framework.TestCaseAttribute>().Any())).OrderBy(t => t.FullName))
            {
                if (filter != null && !t.FullName.Contains(filter)) continue;
                var setup = t.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<NUnit.Framework.SetUpAttribute>() != null);
                var teardown = t.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<NUnit.Framework.TearDownAttribute>() != null);
                foreach (var m in t.GetMethods().OrderBy(m => m.Name))
                {
                    var cases = new List<object[]>();
                    if (m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null) cases.Add(Array.Empty<object>());
                    foreach (var tc in m.GetCustomAttributes<NUnit.Framework.TestCaseAttribute>()) cases.Add(tc.Args);
                    foreach (var c in cases)
                    {
                        var name = $"{t.Name}.{m.Name}({string.Join(",", c)})";
                        try
                        {
                            var inst = Activator.CreateInstance(t);
                            setup?.Invoke(inst, null);
                            var ps = m.GetParameters();
                            var conv = c.Select((a, i) => a == null ? null : Convert.ChangeType(a, ps[i].ParameterType.IsEnum ? Enum.GetUnderlyingType(ps[i].ParameterType) : ps[i].ParameterType)).ToArray();
                            for (int i = 0; i < conv.Length; i++) if (ps[i].ParameterType.IsEnum && conv[i] != null) conv[i] = Enum.ToObject(ps[i].ParameterType, conv[i]);
                            m.Invoke(inst, conv);
                            teardown?.Invoke(inst, null);
                            pass++;
                        }
                        catch (Exception e)
                        {
                            var inner = e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e;
                            Console.WriteLine($"FAIL {name}: {inner.GetType().Name}: {inner.Message}");
                            fail++;
                        }
                    }
                }
            }
            Console.WriteLine($"TESTS: {pass} passed, {fail} failed");
            return fail == 0 ? 0 : 1;
        }
    }
}
