using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace NZNativeContainers
{
    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct ArrayHashMap<TKey, TValue> : IDisposable
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private UnsafeArrayHashMap<TKey, TValue>* _unsafeArrayHashMap;
        
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        [NativeSetClassTypeToNullOnSchedule] private DisposeSentinel m_DisposeSentinel;
#endif
        
        public ArrayHashMap(int initialCapacity, int keyOffset,AllocatorManager.AllocatorHandle allocator)
            : this(initialCapacity, keyOffset, allocator, 2)
        {
        }

        public ArrayHashMap(int initialCapacity, int keyOffset, AllocatorManager.AllocatorHandle allocator, int disposeSentinelStackDepth)
        {
            this = default;
            AllocatorManager.AllocatorHandle temp = allocator;
            Initialize(initialCapacity, keyOffset, ref temp, disposeSentinelStackDepth);
        }

        [BurstCompatible(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        private void Initialize<U>(int initialCapacity, int keyOffset, ref U allocator, int disposeSentinelStackDepth) where U : unmanaged, AllocatorManager.IAllocator
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, disposeSentinelStackDepth, allocator.ToAllocator);
#endif
            
            _unsafeArrayHashMap = UnsafeArrayHashMap<TKey, TValue>.Create(initialCapacity, keyOffset, ref allocator);
            
        }
        
        public bool ContainsKey(TKey key)
        {
            return _unsafeArrayHashMap->TryGetFirstRefValue(key, out var temp0, out var temp1);
        }

        public void SetArrays(NativeArray<TValue> valueArray)
        {
            _unsafeArrayHashMap->SetArrays(valueArray);
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
            if (!UnsafeUtility.IsValidAllocator(_unsafeArrayHashMap->m_Allocator.ToAllocator))
                throw new InvalidOperationException("The NativeArray can not be Disposed because it was not allocated with a valid allocator.");

            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
            
            _unsafeArrayHashMap->Dispose();
        }
        
        public ArrayHashMapEnumerator<TKey, TValue> GetValuesForKey(TKey key)
        {
            return new ArrayHashMapEnumerator<TKey, TValue>
            {
                Map = _unsafeArrayHashMap, 
                key = key, 
                isFirst = true
            };
        }
        
        // helper jobs
        
        public JobHandle CalculateBuckets(NativeArray<TValue> values, JobHandle Dependency)
        {
            return new CalculateBucketsJob()
            {
                hashmap = this,
                values = values
            }.Schedule(Dependency);
        }
        
        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public struct CalculateBucketsJob : IJob
        {
            [ReadOnly] public NativeArray<TValue> values;

            public ArrayHashMap<TKey, TValue> hashmap;

            public void Execute()
            {
                hashmap.SetArrays(values);
            }
        }
    }
}