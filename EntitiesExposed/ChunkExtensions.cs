using System;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Exposed
{
    public static unsafe class ChunkExtensions
    {
        public static void SetChangeVersion<T>(this ArchetypeChunk chunk, ComponentTypeHandle<T> handle)
            where T : struct, IComponentData
        {
            ChunkDataUtility.GetIndexInTypeArray(chunk.m_Chunk->Archetype, handle.m_TypeIndex, ref handle.m_LookupCache);
            
            if (handle.m_LookupCache == -1)
                return;

            chunk.m_Chunk->SetChangeVersion(handle.m_LookupCache, handle.GlobalSystemVersion);
        }
        
        public static void SetChangeVersion<T>(this ArchetypeChunk chunk, BufferTypeHandle<T> handle)
            where T : struct, IBufferElementData
        {
            var typeIndexInArchetype = ChunkDataUtility.GetIndexInTypeArray(chunk.m_Chunk->Archetype, handle.m_TypeIndex);
            if (typeIndexInArchetype == -1)
                return;

            // This should (=S) be thread safe int writes are atomic in c#
            chunk.m_Chunk->SetChangeVersion(typeIndexInArchetype, handle.GlobalSystemVersion);
        }

        public static BufferAccessor<T> GetBufferAccessor<T>(this ArchetypeChunk chunk, BufferTypeHandle<T> bufferComponentTypeHandle, bool bumpVersion = true)
            where T : struct, IBufferElementData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(bufferComponentTypeHandle.m_Safety0);
#endif
            var m_Chunk = chunk.m_Chunk;
            var archetype = m_Chunk->Archetype;
            var typeIndex = bufferComponentTypeHandle.m_TypeIndex;
            var typeIndexInArchetype = ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex);
            if (typeIndexInArchetype == -1)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return new BufferAccessor<T>(null, 0, 0, true, bufferComponentTypeHandle.m_Safety0, bufferComponentTypeHandle.m_Safety1, 0);
#else
                return new BufferAccessor<T>(null, 0, 0, 0);
#endif
            }

            int internalCapacity = archetype->BufferCapacities[typeIndexInArchetype];

            byte* ptr = (bufferComponentTypeHandle.IsReadOnly)
                ? ChunkDataUtility.GetComponentDataRO(m_Chunk, 0, typeIndexInArchetype)
                : GetComponentDataRW(m_Chunk, 0, typeIndexInArchetype, bufferComponentTypeHandle.GlobalSystemVersion, bumpVersion);

            var length = chunk.Count;
            int stride = archetype->SizeOfs[typeIndexInArchetype];
            var batchStartOffset = chunk.m_BatchStartEntityIndex * stride;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new BufferAccessor<T>(ptr + batchStartOffset, length, stride, bufferComponentTypeHandle.IsReadOnly, bufferComponentTypeHandle.m_Safety0, bufferComponentTypeHandle.m_Safety1, internalCapacity);
#else
            return new BufferAccessor<T>(ptr + batchStartOffset, length, stride, internalCapacity);
#endif
        }

        private static byte* GetComponentDataRW(Chunk* chunk, int index, int indexInTypeArray, uint globalSystemVersion, bool bumpVersion = true)
        {
            var offset = chunk->Archetype->Offsets[indexInTypeArray];
            var sizeOf = chunk->Archetype->SizeOfs[indexInTypeArray];

            if (bumpVersion)
            {
                // Write Component to Chunk. ChangeVersion:Yes OrderVersion:No
                chunk->SetChangeVersion(indexInTypeArray, globalSystemVersion);
            }

            return chunk->Buffer + (offset + sizeOf * index);
        }

        public static void* GetComponentDataPtrRW<T>(this ArchetypeChunk chunk, ref ComponentTypeHandle<T> chunkComponentTypeHandle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (chunkComponentTypeHandle.m_IsZeroSized)
                throw new ArgumentException($"ArchetypeChunk.GetNativeArray<{typeof(T)}> cannot be called on zero-sized IComponentData");
            
            AtomicSafetyHandle.CheckWriteAndThrow(chunkComponentTypeHandle.m_Safety);
            if (chunkComponentTypeHandle.IsReadOnly)
                throw new InvalidOperationException(
                    "Provided ComponentTypeHandle is read-only; can't get a read/write pointer to component data");
#endif

            var m_Chunk = chunk.m_Chunk;

            ChunkDataUtility.GetIndexInTypeArray(m_Chunk->Archetype, chunkComponentTypeHandle.m_TypeIndex, ref chunkComponentTypeHandle.m_LookupCache);
            if (chunkComponentTypeHandle.m_LookupCache == -1)
                return null;

            byte* ptr = GetComponentDataRW(m_Chunk, 0, chunkComponentTypeHandle.m_LookupCache, chunkComponentTypeHandle.GlobalSystemVersion);
            var archetype = m_Chunk->Archetype;
            var batchStartOffset = chunk.m_BatchStartEntityIndex * archetype->SizeOfs[chunkComponentTypeHandle.m_LookupCache];
            return ptr + batchStartOffset;
        }
    }
}