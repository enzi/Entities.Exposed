using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace NZNativeContainers.Extensions
{
    public unsafe struct NativeListExtended<T> where T : unmanaged
    {
        public int increaseCount;
        public int currentIndex;

        [NativeDisableUnsafePtrRestriction] public UnsafeList<T>* list;
        [NativeDisableUnsafePtrRestriction] public T* currentPtr;

        public void Add(ref T item)
        {
            if (currentPtr == null || currentIndex >= increaseCount)
            {
                if (ReserveNoResize(list, increaseCount, out currentPtr, out currentIndex))
                {
                    currentIndex = 0;
                }                
                else
                {
                    Debug.LogError("Adding item failed! Could not reserve more memory. Capacity exceeded!");
                    return;
                }
            }

            //UnsafeUtility.WriteArrayElement(currentPtr, currentIndex, item);
            *(T*)((byte*)currentPtr + currentIndex * sizeof(T)) = item;

            //Debug.Log("Writing '" + item.ToString() + "' to index: " + currentIndex + " ptr: " + new IntPtr((void*)currentPtr).ToString("X"));
            currentIndex++;
        }

        public void FillEmpty()
        {
            if (currentPtr == null)
                return;

            while (currentIndex < increaseCount)
            {
                //UnsafeUtility.WriteArrayElement(currentPtr, currentIndex, default(T));
                *(T*)((byte*)currentPtr + currentIndex * sizeof(T)) = default(T);
                currentIndex++;
            }
        }

        public static bool ReserveNoResize<T>(UnsafeList<T>* list, int length, out T* ptr, out int idx)
            where T : unmanaged
        {
            if (list->m_length + length > list->m_capacity)
            {
                idx = 0;
                ptr = null;
                return false;
            }

            idx = Interlocked.Add(ref list->m_length, length) - length;
            ptr = (T*)(((byte*)list->Ptr) + (idx * UnsafeUtility.SizeOf<T>()));

            return true;
        }
    }

    public unsafe static class NativeListExtensions
    {
        public static NativeListExtended<T> GetExtendedList<T>(this ref NativeList<T>.ParallelWriter nativeList, int increaseCount = 10) where T : unmanaged
        {
            NativeListExtended<T> newList = new NativeListExtended<T>
            {
                list = nativeList.ListData,
                increaseCount = increaseCount,
                currentIndex = -1,
                currentPtr = null
            };
            return newList;
        }        
    }
}