using System;
using System.Reflection;

namespace Exerussus.DI
{
    /// <summary>
    /// Предвычисленная точка инъекции. Тип члена, вид и валидность считаются один раз
    /// при построении кэша, чтобы на каждой инъекции не было switch по MemberInfo.
    /// </summary>
    public readonly struct InjectionPoint
    {
        private readonly FieldInfo _field;
        private readonly PropertyInfo _property;

        /// <summary> Тип, который нужно достать из контейнера. </summary>
        public readonly Type MemberType;

        public InjectionPoint(FieldInfo field)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
            _property = null;
            MemberType = field.FieldType;
        }

        public InjectionPoint(PropertyInfo property)
        {
            _property = property ?? throw new ArgumentNullException(nameof(property));
            _field = null;
            MemberType = property.PropertyType;
        }

        public bool IsValid => _field != null || _property != null;
        public MemberInfo Member => (MemberInfo)_field ?? _property;
        public string Name => _field != null ? _field.Name : _property.Name;
        public Type DeclaringType => _field != null ? _field.DeclaringType : _property.DeclaringType;
        public InjectionMemberKind Kind => _field != null ? InjectionMemberKind.Field : InjectionMemberKind.Property;

        public void SetValue(object target, object value)
        {
            if (_field != null) _field.SetValue(target, value);
            else _property.SetValue(target, value);
        }

        public object GetValue(object target)
        {
            return _field != null ? _field.GetValue(target) : _property.GetValue(target);
        }

        public override string ToString()
        {
            return $"{TypeNameUtility.PrettyName(DeclaringType)}.{Name} : {TypeNameUtility.PrettyName(MemberType)}";
        }
    }
}
