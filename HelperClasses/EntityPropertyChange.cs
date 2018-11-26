namespace EntityStore.Net.HelperClasses
{
    public class EntityPropertyChange
    {
        public string PropertyName { get; set; }

        public object OldValue { get; set; }

        public object NewValue { get; set; }
    }
}
