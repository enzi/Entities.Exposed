// <copyright file="NativeMultiHashMapExtensions.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace NZNativeContainers.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    /// <summary> Extensions for <see cref="NativeMultiHashMap{TKey,TValue}"/>. </summary>
    public static class NativeMultiHashMapExtensions2
    {
        public static unsafe void GetKeyAndValueLists<TKey, TValue>(this NativeMultiHashMap<TKey, TValue> hashMap, out NativeList<TKey> keysList, out NativeList<TValue> valuesList)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            //Profiler.BeginSample("get values");
            var keys = hashMap.m_MultiHashMapData.m_Buffer->keys;
            var values = hashMap.m_MultiHashMapData.m_Buffer->values;
            //Profiler.EndSample();

            //Profiler.BeginSample("keysList");
            keysList = default(NativeList<TKey>);
            keysList.m_ListData = hashMap.m_MultiHashMapData.m_AllocatorLabel.Allocate(default(UnsafeList<TKey>), 0); //UnsafeList<TKey>.Create(0, ref hashMap.m_MultiHashMapData.m_AllocatorLabel);
            keysList.m_ListData->Ptr = (TKey*)keys;
            keysList.m_ListData->m_length = 0;
            keysList.m_ListData->m_capacity = hashMap.Capacity;
            //Profiler.EndSample();

            valuesList = default(NativeList<TValue>);
            valuesList.m_ListData = hashMap.m_MultiHashMapData.m_AllocatorLabel.Allocate(default(UnsafeList<TValue>), 0);//UnsafeList<TValue>.Create(0, ref hashMap.m_MultiHashMapData.m_AllocatorLabel);
            valuesList.m_ListData->Ptr = (TValue*)values;
            valuesList.m_ListData->m_length = 0;
            valuesList.m_ListData->m_capacity = hashMap.Capacity;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            //keysList.m_Safety = hashMap.m_Safety;
            //valuesList.m_Safety = hashMap.m_Safety;
            //Profiler.BeginSample("ENABLE_UNITY_COLLECTIONS_CHECKS");
            DisposeSentinel.Create(out keysList.m_Safety, out keysList.m_DisposeSentinel, 2, hashMap.m_MultiHashMapData.m_AllocatorLabel.ToAllocator);
            DisposeSentinel.Create(out valuesList.m_Safety, out valuesList.m_DisposeSentinel, 2, hashMap.m_MultiHashMapData.m_AllocatorLabel.ToAllocator);
            keysList.m_DeprecatedAllocator = hashMap.m_MultiHashMapData.m_AllocatorLabel;
            valuesList.m_DeprecatedAllocator = hashMap.m_MultiHashMapData.m_AllocatorLabel;
            //Profiler.EndSample();
#endif
        }

        public static unsafe void CalculateBuckets<TKey, TValue>(this NativeMultiHashMap<TKey, TValue> hashMap, NativeArray<TKey> keys)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            var data = hashMap.GetUnsafeBucketData();

            var buckets = (int*)data.buckets;
            var nextPtrs = (int*)data.next;

            for (var idx = 0; idx < keys.Length; idx++)
            {
                var bucket = keys[idx].GetHashCode() & data.bucketCapacityMask;
                nextPtrs[idx] = buckets[bucket];
                buckets[bucket] = idx;
            }

            hashMap.m_MultiHashMapData.m_Buffer->allocatedIndexLength = keys.Length;
        }

        public static RefEnumerator<TKey, TValue> GetRefValuesForKey<TKey, TValue>(
            this NativeMultiHashMap<TKey, TValue> hashmap,
            TKey key)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(hashmap.m_Safety);
#endif
            return new RefEnumerator<TKey, TValue> { hashmap = hashmap, key = key, isFirst = true };
        }

        public static unsafe bool ContainsKeyFast<TKey, TValue>(
            this NativeMultiHashMap<TKey, TValue> hashmap,
            TKey key)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            return hashmap.m_MultiHashMapData.TryPeekFirstRefValue(key);
        }

        public static unsafe bool TryPeekFirstRefValue<TKey, TValue>(
            this UnsafeMultiHashMap<TKey, TValue> m_MultiHashMapData,
            TKey key)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            var data = m_MultiHashMapData.m_Buffer;

            if (data->allocatedIndexLength <= 0)
            {
                return false;
            }

            // First find the slot based on the hash
            int* buckets = (int*)data->buckets;
            int bucket = key.GetHashCode() & data->bucketCapacityMask;
            return m_MultiHashMapData.TryPeekNextRefValue(key, buckets[bucket]);
        }

        public static unsafe bool TryPeekNextRefValue<TKey, TValue>(
            this UnsafeMultiHashMap<TKey, TValue> m_MultiHashMapData,
            TKey key,
            int entryIdx)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            //CheckRead();

            var data = m_MultiHashMapData.m_Buffer;
            if (entryIdx < 0 || entryIdx >= data->keyCapacity)
            {
                return false;
            }

            int* nextPtrs = (int*)data->next;
            while (!(*(TKey*)(data->keys + entryIdx * sizeof(TKey))).Equals(key))
            {
                entryIdx = nextPtrs[entryIdx];
                if (entryIdx < 0 || entryIdx >= data->keyCapacity)
                {
                    return false;
                }
            }

            return true;
        }


        public unsafe struct RefEnumerator<TKey, TValue> 
            where TKey : unmanaged, IEquatable<TKey> 
            where TValue : unmanaged
        {
            public NativeMultiHashMap<TKey, TValue> hashmap;
            public TKey key;
            public bool isFirst;

            byte* value;
            NativeMultiHashMapIterator<TKey> iterator;

            /// <summary>
            /// Does nothing.
            /// </summary>
            public void Dispose() { }

            /// <summary>
            /// Advances the enumerator to the next value of the key.
            /// </summary>
            /// <returns>True if <see cref="Current"/> is valid to read after the call.</returns>
            public bool MoveNext()
            {
                //Avoids going beyond the end of the collection.
                if (isFirst)
                {
                    isFirst = false;                    
                    return hashmap.m_MultiHashMapData.TryGetFirstRefValue(key, ref value, out iterator);
                }

                return hashmap.m_MultiHashMapData.TryGetNextRefValue(ref value, ref iterator);
            }

            /// <summary>
            /// Resets the enumerator to its initial state.
            /// </summary>
            public void Reset() => isFirst = true;

            /// <summary>
            /// The current value.
            /// </summary>
            /// <value>The current value.</value>
            public ref TValue Current => ref UnsafeUtility.AsRef<TValue>(value);
        }        

        public static unsafe bool TryGetFirstRefValue<TKey, TValue>(
            this UnsafeMultiHashMap<TKey, TValue> m_MultiHashMapData,
            TKey key,
            ref byte* item, 
            out NativeMultiHashMapIterator<TKey> it)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            it.key = key;
            var data = m_MultiHashMapData.m_Buffer;

            if (data->allocatedIndexLength <= 0)
            {
                it.EntryIndex = it.NextEntryIndex = -1;
                item = default;
                return false;
            }

            // First find the slot based on the hash
            int* buckets = (int*)data->buckets;
            int bucket = key.GetHashCode() & data->bucketCapacityMask;
            it.EntryIndex = it.NextEntryIndex = buckets[bucket];
            return m_MultiHashMapData.TryGetNextRefValue(ref item, ref it);
        }       

        public static unsafe bool TryGetNextRefValue<TKey, TValue>(
            this UnsafeMultiHashMap<TKey, TValue> m_MultiHashMapData,
            ref byte* item,
            ref NativeMultiHashMapIterator<TKey> it)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            //CheckRead();
            int entryIdx = it.NextEntryIndex;
            it.NextEntryIndex = -1;
            it.EntryIndex = -1;
            item = default;

            var data = m_MultiHashMapData.m_Buffer;
            if (entryIdx < 0 || entryIdx >= data->keyCapacity)
            {
                return false;
            }

            int* nextPtrs = (int*)data->next;
            while (!(*(TKey*) (data->keys + entryIdx * sizeof(TKey))).Equals(it.key))
            {
                entryIdx = nextPtrs[entryIdx];
                if (entryIdx < 0 || entryIdx >= data->keyCapacity)
                {
                    return false;
                }
            }

            it.NextEntryIndex = nextPtrs[entryIdx];
            it.EntryIndex = entryIdx;

            // Read the value
            item = data->values + entryIdx * sizeof(TValue);

            return true;
        }
    }
}
