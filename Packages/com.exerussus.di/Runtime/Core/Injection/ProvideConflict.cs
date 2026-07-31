using System;

namespace Exerussus.DI
{
    /// <summary> Описание конфликта [Provide]-члена с уже зарегистрированным типом. </summary>
    public readonly struct ProvideConflict
    {
        public readonly Type TargetType;
        public readonly Type ConflictingType;
        public readonly string MemberName;

        public ProvideConflict(Type targetType, in InjectionPoint point)
        {
            TargetType = targetType;
            ConflictingType = point.MemberType;
            MemberName = point.Name;
        }

        public bool IsEmpty => ConflictingType == null;

        public override string ToString()
        {
            if (IsEmpty) return "[DI] Конфликтов нет.";
            return $"[DI] {TypeNameUtility.PrettyName(TargetType)}.{MemberName} помечено [Provide], " +
                   $"но тип {TypeNameUtility.PrettyName(ConflictingType)} уже зарегистрирован в этом контейнере.";
        }
    }
}
