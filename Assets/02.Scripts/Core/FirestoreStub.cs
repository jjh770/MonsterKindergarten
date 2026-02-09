#if UNITY_WEBGL && !UNITY_EDITOR
namespace Firebase.Firestore
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public class FirestoreDataAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public class FirestorePropertyAttribute : System.Attribute
    {
        public FirestorePropertyAttribute() { }
        public FirestorePropertyAttribute(string name) { }
    }
}
#endif
