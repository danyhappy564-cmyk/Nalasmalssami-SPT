using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Nalasmalssami.Util
{
    /// <summary>게임·TextMeshPro 멤버를 이름으로 찾는 도우미. 못 찾으면 null을 돌려준다.</summary>
    internal static class Reflect
    {
        public const BindingFlags Inst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        public const BindingFlags Stat = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        public static Type Type(string fullName) => AccessTools.TypeByName(fullName);

        /// <summary>상속받은 private 필드까지 찾는다(AccessTools는 부모 클래스를 거슬러 올라간다).</summary>
        public static FieldInfo Field(Type t, string name) => t == null ? null : AccessTools.Field(t, name);

        public static PropertyInfo Prop(Type t, string name) => t == null ? null : AccessTools.Property(t, name);

        public static MethodInfo Method(Type t, string name, params Type[] args) =>
            t == null ? null : t.GetMethod(name, Inst | Stat, null, args, null);

        /// <summary>이름과 파라미터 개수로 찾는다(오버로드 중 하나만 맞으면 됨).</summary>
        public static MethodInfo MethodByArity(Type t, string name, int arity) =>
            t?.GetMethods(Inst | Stat).FirstOrDefault(m => m.Name == name && m.GetParameters().Length == arity);

        /// <summary>UnityEngine.Object는 Equals를 덮어써서, 파괴된 객체도 null처럼 취급된다.</summary>
        public static bool IsNull(object o) => o == null || o.Equals(null);

        public static string Missing(params (string name, object member)[] members) =>
            string.Join(", ", members.Where(m => m.member == null).Select(m => m.name));
    }
}
