using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Debug = UnityEngine.Debug;

namespace NZNativeContainers
{
    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct ParallelList<T> : IDisposable
        where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeParallelList<T>* _unsafeParallelList;
        
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        internal int m_SafetyIndexHint;
        internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<ParallelList<T>>();
#endif
        
        public int Length => _unsafeParallelList->Count();
        
        public ParallelList(AllocatorManager.AllocatorHandle allocator)
            : this(1, allocator)
        {
        }

        public ParallelList(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            this = default;
            AllocatorManager.AllocatorHandle temp = allocator;
            Initialize(initialCapacity, ref temp);
        }
        
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        private void Initialize<U>(int initialCapacity, ref U allocator) where U : unmanaged, AllocatorManager.IAllocator
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var totalSize = sizeof(T) * (long)initialCapacity;
            CollectionHelper.CheckAllocator(allocator.Handle);
            CheckInitialCapacity(initialCapacity);
            CheckTotalSize(initialCapacity, totalSize);

            m_Safety = CollectionHelper.CreateSafetyHandle(allocator.Handle);
            CollectionHelper.InitNativeContainer<T>(m_Safety);

            CollectionHelper.SetStaticSafetyId<ParallelList<T>>(ref m_Safety, ref s_staticSafetyId.Data);

            m_SafetyIndexHint = (allocator.Handle).AddSafetyHandle(m_Safety);

            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
            
            _unsafeParallelList = UnsafeParallelList<T>.Create(initialCapacity, ref allocator);
            
        }

        public int GetChunkCount()
        {
            return _unsafeParallelList->GetChunkCount();
        }

        public void SetChunkCount(int chunkCount)
        {
            _unsafeParallelList->SetChunkCount(chunkCount);
        }

        public ref UnsafeList<T> GetUnsafeList(int threadIndex)
        {
            return ref _unsafeParallelList->GetUnsafeList(threadIndex);
        }

        public void Clear()
        {
            _unsafeParallelList->Clear();
        }
        
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckInitialCapacity(int initialCapacity)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Capacity must be >= 0");
        }
        
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckTotalSize(int initialCapacity, long totalSize)
        {
            // Make sure we cannot allocate more than int.MaxValue (2,147,483,647 bytes)
            // because the underlying UnsafeUtility.Malloc is expecting a int.
            // TODO: change UnsafeUtility.Malloc to accept a UIntPtr length instead to match C++ API
            if (totalSize > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), $"Capacity * sizeof(T) cannot exceed {int.MaxValue} bytes");
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif
            UnsafeParallelList<T>.Destroy(_unsafeParallelList);
            _unsafeParallelList = null;
        }
        
        public Reader AsReader()
        {
            return new Reader(ref this);
        }

        public Writer AsWriter()
        {
            return new Writer(ref this);
        }

        [NativeContainer]
        [GenerateTestsForBurstCompatibility]
        [NativeContainerIsAtomicWriteOnly]
        public unsafe struct Writer
        {
            private UnsafeParallelList<T>.Writer m_Writer;
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle m_Safety;
            internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<Writer>();
#endif
            
            internal Writer(ref ParallelList<T> parallelList)
            {
                m_Writer = parallelList._unsafeParallelList->AsWriter();;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = parallelList.m_Safety;
                CollectionHelper.SetStaticSafetyId(ref m_Safety, ref s_staticSafetyId.Data, "Unity.Collections.NativeStream.Writer");

                if (parallelList._unsafeParallelList->CheckRangesForNull())
                    Debug.LogError($"Ranges have not been allocated. SetChunkCount(int chunkCount) before writing something.");// {Environment.StackTrace}");
#endif
            }
            
            public void BeginForEachChunk(int chunkIndex)
            {
                m_Writer.BeginForEachChunk(chunkIndex);
            }
            
            public void Write(in T value)
            {
                m_Writer.Write(in value);
            }
            
            public void WriteMemCpy(ref T value)
            {
                m_Writer.WriteMemCpy(ref value);
            }
            
            public void EndForEachChunk()
            {
                m_Writer.EndForEachChunk();
            }

            public void SetManualThreadIndex(int threadIndex)
            {
                m_Writer.SetManualThreadIndex(threadIndex);
            }
        }

        [NativeContainer]
        [NativeContainerIsReadOnly]
        [GenerateTestsForBurstCompatibility]
        public unsafe struct Reader
        {
            private UnsafeParallelList<T>.Reader m_Reader;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            int m_RemainingBlocks;
            internal AtomicSafetyHandle m_Safety;
            internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<Reader>();
            
#endif

            internal Reader(ref ParallelList<T> stream)
            {
                m_Reader = stream._unsafeParallelList->AsReader();;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_RemainingBlocks = 0;
                m_Safety = stream.m_Safety;
                CollectionHelper.SetStaticSafetyId(ref m_Safety, ref s_staticSafetyId.Data, "Unity.Collections.NativeStream.Reader");
#endif
            }
            
            public int BeginForEachChunk(int chunkIndex)
            {
                return m_Reader.BeginForEachChunk(chunkIndex);
            }
            
            public ref T Read()
            {
                return ref m_Reader.Read();
            }

            public T* GetPtr()
            {
                return m_Reader.GetPtr();
            }

            public void Reset(int chunkIndex)
            {
                m_Reader.Reset(chunkIndex);
            }

            public int GetListIndex(int chunkIndex)
            {
                return m_Reader.GetListIndex(chunkIndex);
            }
        }
        
        // helper jobs

        // public JobHandle CopyParallelListToArrayMultiThreaded(ref NativeList<T> writer, JobHandle Dependency)
        // {
        //     return new ParallelListToArrayMultiThreaded()
        //     {
        //         parallelList = this,
        //         array = writer.AsParallelWriter()
        //     }.ScheduleParallel(JobsUtility.MaxJobThreadCount, 1, Dependency);
        // }
        
        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        private struct ParallelListToArrayMultiThreaded : IJobFor
        {
            [ReadOnly] public ParallelList<T> parallelList;
            public NativeList<T>.ParallelWriter array;
        
            public void Execute(int index)
            {
                ref var threadList = ref parallelList.GetUnsafeList(index);
                array.AddRangeNoResize(threadList.Ptr, threadList.m_length);
            }
        }
        
        public JobHandle CopyParallelListToArraySingleThreaded(ref NativeList<T> nativeList, JobHandle Dependency)
        {
            return new ParallelListToArraySingleThreaded()
            {
                parallelList = this,
                array = nativeList
            }.Schedule(Dependency);
        }
        
        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        private struct ParallelListToArraySingleThreaded : IJob
        {
            [ReadOnly] public ParallelList<T> parallelList;
            public NativeList<T> array;
        
            public void Execute()
            {
                if (array.Capacity < array.Length + parallelList.Length)
                    array.Capacity = array.Length + parallelList.Length;
                
                for (int i = 0; i < JobsUtility.MaxJobThreadCount; i++)
                {
                    ref var threadList = ref parallelList.GetUnsafeList(i);
                    array.AddRangeNoResize(threadList.Ptr, threadList.m_length);
                }
            }
        }
    }
}