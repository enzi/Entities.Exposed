using Unity.Collections;

namespace Unity.Entities.Exposed
{
    public static unsafe class FixedListExtensions
    {
        public static T* GetPtr<T>(this FixedList4096Bytes<T> fixedList)
            where T : unmanaged
        {
            return (T*) fixedList.Buffer;
        }
    }
}