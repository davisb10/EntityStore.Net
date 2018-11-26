namespace EntityStore.Net.Utilities
{
    public class Projections
    {
        public static string PROJECTION_EVENT_TYPE =
@"fromStream('$streams').
    when({
        $init: function() {
            return {
                streamIds : []
            };
        },
        $any: function(s, e) {
        
            if (e.data !== null && e.body !== null) {
                var eventType = String(e.eventType);
            
                if (eventType.toLowerCase() === '{{eventType}}'){
                    s.streamIds.push(e.streamId);
                }
            }
            
        }
});";
    }
}
