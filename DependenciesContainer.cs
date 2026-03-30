using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Exerussus.DI
{
    public class DependenciesContainer
    {
        public DependenciesContainer() { }

        public DependenciesContainer(DependenciesContainer container)
        {
            foreach (var (key, value) in container._refs) _refs[key] = value;
        }

        private readonly Dictionary<Type, object> _refs = new();

        private static readonly Exception ServiceGetException = new($"[DependenciesContainer] Целевой объект отсутствует в коллекции. Референс не был добавлен?");
        private static readonly Exception ServiceCastException = new($"[DependenciesContainer] Невозможно скастить найденный объект в необходимый тип.");
        private static readonly Type InjectAttrType = typeof(InjectAttribute);
        private static readonly Type ProvideAttrType = typeof(ProvideAttribute);

        public void Clear()
        {
            _refs.Clear();
        }

        public DependenciesContainer Add(params object[] refs)
        {
            if (refs is not { Length: > 0 })
            {
                Debug.LogError($"DependenciesContainer ERROR | Параметры содержат null.");
                return this;
            }

            for (int i = 0; i < refs.Length; i++)
            {
                ref var refObject = ref refs[i];

                var type = refObject.GetType();

                if (_refs.ContainsKey(type))
                {
                    Debug.LogWarning($"DependenciesContainer WARNING | Тип {type} уже был добавлен. Референс будет перезаписан.");
                }

                _refs[type] = refObject;
            }

            return this;
        }

        public DependenciesContainer AddRange(object[] refs)
        {
            if (refs is not { Length: > 0 })
            {
                Debug.LogError($"DependenciesContainer ERROR | Параметры содержат null.");
                return this;
            }

            for (int i = 0; i < refs.Length; i++)
            {
                ref var refObject = ref refs[i];

                var type = refObject.GetType();

                if (_refs.ContainsKey(type))
                {
                    Debug.LogWarning($"DependenciesContainer WARNING | Тип {type} уже был добавлен. Референс будет перезаписан.");
                }

                _refs[type] = refObject;
            }

            return this;
        }

        public object[] GetAllRefs()
        {
            return _refs.Values.ToArray();
        }

        public T Get<T>()
        {
            var type = typeof(T);

            if (!_refs.TryGetValue(type, out var serviceRaw))
            {
                Debug.LogError($"{ServiceGetException.Message}\nType: {type}");
                throw ServiceGetException;
            }

            if (serviceRaw is not T service)
            {
                Debug.LogError($"{ServiceCastException.Message}\nObject type: {serviceRaw.GetType()}\nTarget type: {type}");
                throw ServiceCastException;
            }

            return service;
        }

        public DependenciesContainer Remove<T>()
        {
            _refs.Remove(typeof(T));
            return this;
        }

        public DependenciesContainer Remove(Type type)
        {
            _refs.Remove(type);
            return this;
        }

        public DependenciesContainer Remove(object obj)
        {
            _refs.Remove(obj.GetType());
            return this;
        }

        public bool TryGet<T>(out T value)
        {
            if (_refs.TryGetValue(typeof(T), out var obj))
            {
                value = (T)obj;
                return value != null;
            }

            value = default;
            return false;
        }

        public void Merge(DependenciesContainer otherContainer)
        {
            foreach (var reference in otherContainer._refs.Values) Add(reference);
        }

        private bool TryGet(Type type, out object value)
        {
            return _refs.TryGetValue(type, out value);
        }

        public void TryInjectFields(object target, DependenciesContainer otherContainer = null)
        {
            foreach (var fi in target.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (fi.IsStatic) continue;

                if (Attribute.IsDefined(fi, InjectAttrType))
                {
                    if (TryGet(fi.FieldType, out var injectObj))
                    {
                        fi.SetValue(target, injectObj);
                    }
                    else if (otherContainer != null && otherContainer.TryGet(fi.FieldType, out injectObj))
                    {
                        fi.SetValue(target, injectObj);
                    }
                    else
                    {
#if DEBUG
                        throw new Exception(
                            $"Ошибка инъекции данных в \"{CleanTypeName(target.GetType())}\" - тип {fi.FieldType.Name} поля \"{fi.Name}\" отсутствует в контейнере зависимостей.");
#endif
                    }
                }
            }
        }

        public void TryInjectFields(object target, Func<Type, (bool isError, object instance)> onInstanceNotExist, DependenciesContainer otherContainer = null)
        {
            foreach (var fi in target.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (fi.IsStatic) continue;

                if (Attribute.IsDefined(fi, InjectAttrType))
                {
                    if (TryGet(fi.FieldType, out var injectObj))
                    {
                        fi.SetValue(target, injectObj);
                    }
                    else if (otherContainer != null && otherContainer.TryGet(fi.FieldType, out injectObj))
                    {
                        fi.SetValue(target, injectObj);
                    }
                    else
                    {
                        var (isError, instance) = onInstanceNotExist.Invoke(fi.FieldType);

                        if (isError)
                        {
#if DEBUG
                            throw new Exception(
                                $"Ошибка инъекции данных в \"{CleanTypeName(target.GetType())}\" - тип {fi.FieldType.Name} поля \"{fi.Name}\" отсутствует в контейнере зависимостей.");
#endif
                        }

                        if (instance != null)
                        {
                            fi.SetValue(target, instance);
                            _refs[fi.FieldType] = instance;
                        }
                    }
                }
            }
        }

        public void TryProvideFields(object target)
        {
            foreach (var fi in target.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (fi.IsStatic) continue;

                if (!Attribute.IsDefined(fi, ProvideAttrType)) continue;

                var value = fi.GetValue(target);

                if (value == null)
                {
#if DEBUG
                    throw new Exception($"Provide ERROR | Поле \"{fi.Name}\" в \"{CleanTypeName(target.GetType())}\" помечено [Provide], но содержит null.");
#else
            continue;
#endif
                }

                var type = fi.FieldType;

                if (_refs.ContainsKey(type))
                {
                    Debug.LogWarning($"DependenciesContainer WARNING | Тип {type} уже существует. Provide перезапишет значение.");
                }

                _refs[type] = value;
            }
        }

        public void TryInvokeExceptionOnProvideFields(object target, string onExistExceptionMessage)
        {
            foreach (var fi in target.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (fi.IsStatic) continue;

                if (!Attribute.IsDefined(fi, ProvideAttrType)) continue;

                var value = fi.GetValue(target);

                throw new Exception($"DependenciesContainer.TryInvokeExceptionOnProvideFields EXCEPTION | {onExistExceptionMessage} \nОбъект: {target}\nПоле: {fi.Name}");
            }
        }

#if DEBUG || UNITY_EDITOR
        private static string CleanTypeName(Type type)
        {
            string name;
            if (!type.IsGenericType) name = type.Name;
            else
            {
                var constraints = new StringBuilder();
                foreach (var constraint in type.GetGenericArguments())
                {
                    if (constraints.Length > 0) constraints.Append(", ");

                    constraints.Append(CleanTypeName(constraint));
                }

                var genericIndex = type.Name.LastIndexOf("`", StringComparison.Ordinal);
                var typeName = genericIndex == -1
                    ? type.Name
                    : type.Name.Substring(0, genericIndex);
                name = $"{typeName}<{constraints}>";
            }

            return name;
        }
#endif
    }
}