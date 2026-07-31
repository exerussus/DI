using System;

namespace Exerussus.DI
{
    /// <summary> Описание неразрешённой точки инъекции. Возвращается из TryInject. </summary>
    public readonly struct MissingDependency
    {
        public readonly Type TargetType;
        public readonly Type MissingType;
        public readonly string MemberName;

        public MissingDependency(Type targetType, in InjectionPoint point)
        {
            TargetType = targetType;
            MissingType = point.MemberType;
            MemberName = point.Name;
        }

        public bool IsEmpty => MissingType == null;

        public override string ToString()
        {
            if (IsEmpty) return "[DI] Нет неразрешённых зависимостей.";
            return $"[DI] Не удалось внедрить {TypeNameUtility.PrettyName(MissingType)} " +
                   $"в {TypeNameUtility.PrettyName(TargetType)}.{MemberName}: тип не зарегистрирован в контейнере.";
        }
    }
}
