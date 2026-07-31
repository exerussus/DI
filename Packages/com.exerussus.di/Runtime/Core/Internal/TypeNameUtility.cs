using System;
using System.Text;

namespace Exerussus.DI
{
    /// <summary> Читаемые имена типов для сообщений об ошибках. Только stateless-функции. </summary>
    public static class TypeNameUtility
    {
        /// <summary> List`1 превращает в List&lt;Foo&gt;. Вызывается только на путях ошибок. </summary>
        public static string PrettyName(Type type)
        {
            if (type == null) return "<null>";
            if (!type.IsGenericType) return type.Name;

            var builder = new StringBuilder();
            Append(builder, type);
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, Type type)
        {
            if (!type.IsGenericType)
            {
                builder.Append(type.Name);
                return;
            }

            var name = type.Name;
            var tick = name.LastIndexOf('`');
            builder.Append(tick == -1 ? name : name.Substring(0, tick));
            builder.Append('<');

            var arguments = type.GetGenericArguments();
            for (var i = 0; i < arguments.Length; i++)
            {
                if (i > 0) builder.Append(", ");
                Append(builder, arguments[i]);
            }

            builder.Append('>');
        }
    }
}
