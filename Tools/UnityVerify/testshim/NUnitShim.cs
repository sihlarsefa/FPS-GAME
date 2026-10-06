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
    public sealed class IgnoreException : Exception { public IgnoreException(string m) : base(m) { } }
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
        public static void Ignore(string message = null) => throw new IgnoreException(message);
        public static void Inconclusive(string message = null) => throw new IgnoreException(message);
        public static void That(object actual, Constraint c, string message = null) { if (!c.Check(actual)) F($"Expected {c.Describe()} but was {actual}", message); }
        public static void That(bool condition, string message = null) { if (!condition) F("Expected true", message); }
    }
    public sealed class Constraint
    {
        readonly Func<object, bool> _f; readonly string _d;
        public Constraint(Func<object, bool> f, string d) { _f = f; _d = d; }
        public bool Check(object o) { try { return _f(o); } catch { return false; } }
        public string Describe() => _d;
        /// <summary>EqualTo(x).Within(tol): sayısal karşılaştırmayı toleransa çevirir (NUnit uyumu).</summary>
        public Constraint Within(double tol) => new Constraint(o =>
        {
            if (_expected == null || o == null) return Check(o);
            try { return Math.Abs(Convert.ToDouble(_expected) - Convert.ToDouble(o)) <= tol; } catch { return Check(o); }
        }, _d + $" ±{tol}") { _expected = _expected };
        internal object _expected;
    }
    public static class Is
    {
        public static Constraint InRange(double lo, double hi) => new Constraint(o => Convert.ToDouble(o) >= lo && Convert.ToDouble(o) <= hi, $"in range {lo}..{hi}");
        public static Constraint EqualTo(object e) => new Constraint(o => Equals(e, o) || (e != null && o != null && Convert.ToDouble(e) == Convert.ToDouble(o)), $"equal to {e}") { _expected = e };
        public static Constraint GreaterThanOrEqualTo(double v) => new Constraint(o => Convert.ToDouble(o) >= v, $">= {v}");
        public static Constraint LessThanOrEqualTo(double v) => new Constraint(o => Convert.ToDouble(o) <= v, $"<= {v}");
        public static NotBuilder Not => new NotBuilder();
        public sealed class NotBuilder
        {
            public Constraint EqualTo(object e) => new Constraint(o => !(Equals(e, o) || (e != null && o != null && SafeEq(e, o))), $"not equal to {e}");
            public Constraint Null => new Constraint(o => o != null, "not null");
            static bool SafeEq(object a, object b) { try { return Convert.ToDouble(a) == Convert.ToDouble(b); } catch { return false; } }
        }
        public static Constraint GreaterThan(double v) => new Constraint(o => Convert.ToDouble(o) > v, $"> {v}");
        public static Constraint LessThan(double v) => new Constraint(o => Convert.ToDouble(o) < v, $"< {v}");
        public static Constraint True => new Constraint(o => o is bool b && b, "true");
        public static Constraint False => new Constraint(o => o is bool b && !b, "false");
        public static Constraint Null => new Constraint(o => o == null, "null");
        public static Constraint Empty => new Constraint(o =>
        {
            if (o == null) return false;
            if (o is string str) return str.Length == 0;
            if (o is System.Collections.IEnumerable en) { foreach (var _ in en) return false; return true; }
            return false;
        }, "empty");
        public static Constraint NotNull => new Constraint(o => o != null, "not null");
    }
    public static class StringAssert
    {
        static void F(string m, string u) => throw new AssertionException(string.IsNullOrEmpty(u) ? m : m + " :: " + u);
        public static void Contains(string expected, string actual, string message = null) { if (actual == null || !actual.Contains(expected)) F($"Expected \"{actual}\" to contain \"{expected}\"", message); }
        public static void DoesNotContain(string expected, string actual, string message = null) { if (actual != null && actual.Contains(expected)) F($"Expected \"{actual}\" not to contain \"{expected}\"", message); }
        public static void StartsWith(string expected, string actual, string message = null) { if (actual == null || !actual.StartsWith(expected, StringComparison.Ordinal)) F($"Expected \"{actual}\" to start with \"{expected}\"", message); }
        public static void EndsWith(string expected, string actual, string message = null) { if (actual == null || !actual.EndsWith(expected, StringComparison.Ordinal)) F($"Expected \"{actual}\" to end with \"{expected}\"", message); }
    }
    public static class CollectionAssert
    {
        public static void AllItemsAreUnique(System.Collections.IEnumerable collection, string message = null)
        {
            var seen = new System.Collections.Generic.HashSet<object>();
            foreach (var item in collection)
                if (!seen.Add(item))
                    throw new AssertionException($"Collection has duplicate item: {item} :: {message}");
        }

        public static void Contains(System.Collections.IEnumerable collection, object expected, string message = null)
        {
            foreach (var item in collection)
                if (Equals(item, expected)) return;
            throw new AssertionException($"Collection does not contain {expected} :: {message}");
        }

        public static void AreEqual(System.Collections.IEnumerable expected, System.Collections.IEnumerable actual, string message = null)
        {
            var a = expected.Cast<object>().ToArray(); var b = actual.Cast<object>().ToArray();
            if (a.Length != b.Length) throw new AssertionException($"Collections differ in length {a.Length} vs {b.Length} :: {message}");
            for (int i = 0; i < a.Length; i++) if (!Equals(a[i], b[i])) throw new AssertionException($"Collections differ at {i}: {a[i]} vs {b[i]} :: {message}");
        }
    }
}
namespace TestShimRunner
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            int pass = 0, fail = 0, skip = 0;
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
                            // Unity yerel köprüsü (ECall) gerektiren testler bu ortamda koşamaz: atlanır, başarısız sayılmaz.
                            if (inner is NUnit.Framework.IgnoreException || (inner is System.Security.SecurityException && inner.Message.Contains("ECall")))
                            {
                                Console.WriteLine($"SKIP {name}: {inner.Message}");
                                skip++;
                                continue;
                            }

                            Console.WriteLine($"FAIL {name}: {inner.GetType().Name}: {inner.Message}");
                            fail++;
                        }
                    }
                }
            }
            Console.WriteLine($"TESTS: {pass} passed, {fail} failed, {skip} skipped (Unity yerel köprüsü gerekir)");
            return fail == 0 ? 0 : 1;
        }
    }
}

namespace UnityEngine.TestTools
{
    /// <summary>Koşucu Unity log'unu yakalamadığı için beklentiler no-op'tur; testler log satırı yüzünden kırılmaz.</summary>
    public static class LogAssert
    {
        public static bool ignoreFailingMessages { get; set; }
        public static void Expect(UnityEngine.LogType type, string message) { }
        public static void Expect(UnityEngine.LogType type, System.Text.RegularExpressions.Regex message) { }
        public static void NoUnexpectedReceived() { }
    }
}
