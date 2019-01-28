using System.Collections.ObjectModel;
using System.Reflection;

namespace EntityStore.Net
{
    public class PropertyComparator
    {
        public static Collection<EntityPropertyChange> Compare<T>(T oldObject, T newObject) where T : class
        {
            Collection<EntityPropertyChange> propertyChanges = new Collection<EntityPropertyChange>();
            PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            object _oldValue;
            object _newValue;

            foreach (PropertyInfo property in properties)
            {
                _oldValue = property.GetValue(oldObject);
                _newValue = property.GetValue(newObject);

                EntityPropertyChange propertyChange = new EntityPropertyChange
                {
                    PropertyName = property.Name,
                    OldValue = _oldValue,
                    NewValue = _newValue
                };
                if (_oldValue == null && _newValue == null)
                {
                    continue;
                }
                if ((_oldValue == null && _newValue != null) || (_oldValue != null && _newValue == null))
                {
                    propertyChanges.Add(propertyChange);
                    continue;
                }
                if (!_oldValue.Equals(_newValue))
                {
                    propertyChanges.Add(propertyChange);
                }
            }
            return propertyChanges;
        }
    }
}
