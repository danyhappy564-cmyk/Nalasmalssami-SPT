using System;
using System.Linq;
using System.Reflection;

namespace Nalasmalssami.Util
{
    /// <summary>아이템(Item)의 태그 글자를 읽는다 — Item.GetItemComponent&lt;TagComponent&gt;().Name</summary>
    internal static class TagAccess
    {
        private static MethodInfo _getTag;
        private static FieldInfo _name;
        private static bool _resolved;

        public static bool Ready
        {
            get
            {
                Resolve();
                return _getTag != null && _name != null;
            }
        }

        /// <summary>태그가 없거나 공백뿐이면 null.</summary>
        public static string GetTagName(object item)
        {
            if (item == null || !Ready)
            {
                return null;
            }

            object tag = _getTag.Invoke(item, null);
            string name = tag == null ? null : _name.GetValue(tag) as string;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        private static void Resolve()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            Type item = Reflect.Type("EFT.InventoryLogic.Item");
            Type tag = Reflect.Type("EFT.InventoryLogic.TagComponent");
            if (item == null || tag == null)
            {
                return;
            }

            MethodInfo generic = item.GetMethods(Reflect.Inst).FirstOrDefault(m =>
                m.Name == "GetItemComponent" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
            _getTag = generic?.MakeGenericMethod(tag);
            _name = tag.GetField("Name", Reflect.Inst);
        }
    }
}
