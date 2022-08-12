using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NZNativeContainers
{
    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct ParallelListHashMap<TKey, TValue> : IDisposable
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeParallelListHashMap<TKey, TValue>* _unsafeParallelListHashMap;
        
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        [NativeSetClassTypeToNullOnSchedule] internal DisposeSentinel m_DisposeSentinel;
#endif

        public ParallelListHashMap(AllocatorManager.AllocatorHandle allocator)
            : this(1, allocator, 2)
        {
        }
        
        public ParallelListHashMap(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
            : this(initialCapacity, allocator, 2)
        {
        }
        
        ParallelListHashMap(int initialCapacity, AllocatorManager.AllocatorHandle allocator, int disposeSentinelStackDepth)
        {
            this = default;
            AllocatorManager.AllocatorHandle temp = allocator;
            Initialize(initialCapacity, ref temp, disposeSentinelStackDepth);
        }

        [BurstCompatible(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal void Initialize<U>(int initialCapacity, ref U allocator, int disposeSentinelStackDepth) where U : unmanaged, AllocatorManager.IAllocator
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, disposeSentinelStackDepth, allocator.ToAllocator);
#endif
            
            _unsafeParallelListHashMap = UnsafeParallelListHashMap<TKey, TValue>.Create(initialCapacity, ref allocator);
            
        }
        
        public bool ContainsKey(TKey key)
        {
            return _unsafeParallelListHashMap->TryGetFirstRefValue(key, out var temp0, out var temp1);
        }

        public void SetArrays(UnsafeParallelList<TKey> keyArray, UnsafeParallelList<TValue> valueArray)
        {
            _unsafeParallelListHashMap->SetArrays(keyArray, valueArray);
        }
        
        // public void PrintValues()
        // {
        //     //Debug.Log($"PrintValues with length {allocatedIndexLength}");
        //     for (int i = 0; i < allocatedIndexLength; i++)
        //     {
        //         var key = (*(TKey*)(Keys + i * sizeof(TKey)));
        //         Debug.Log($"Key: {key}");
        //     }
        //     for (int i = 0; i < allocatedIndexLength; i++)
        //     {
        //         var value = (*(TValue*)(Values + i * sizeof(TValue)));
        //         Debug.Log($"value: {value}");
        //     }
        //     for (int i = 0; i < allocatedIndexLength; i++)
        //     {
        //         var nextValue = (*(int*)(next + i * sizeof(int)));
        //         Debug.Log($"nextValue: {nextValue}");
        //     }
        //     for (int i = 0; i < (bucketCapacityMask + 1); i++)
        //     {
        //         var bucketValue = (*(int*)(buckets + i * sizeof(int)));
        //         Debug.Log($"bucketValue: {bucketValue}");
        //     }
        // }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!UnsafeUtility.IsValidAllocator(_unsafeParallelListHashMap->m_Allocator.ToAllocator))
                throw new InvalidOperationException("The NativeArray can not be Disposed because it was not allocated with a valid allocator.");

            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
            
            _unsafeParallelListHashMap->Dispose();
        }
        
        public UnsafeParallelListHashMapEnumerator<TKey, TValue> GetValuesForKey(TKey key)
        {
            return new UnsafeParallelListHashMapEnumerator<TKey, TValue>
            {
                Map = _unsafeParallelListHashMap, 
                key = key, 
                isFirst = true
            };
        }
    }
}