using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Exposed
{
    public unsafe struct EntityLocationInChunk : IEquatable<EntityLocationInChunk>, IComparable<EntityLocationInChunk>
    {
        public ArchetypeChunk chunk;
        public int            indexInChunk;

        public ulong ChunkAddressAsUlong => (ulong)chunk.m_Chunk;

        public int CompareTo(EntityLocationInChunk other)
        {
            ulong lhs          = (ulong)chunk.m_Chunk;
            ulong rhs          = (ulong)other.chunk.m_Chunk;
            int   chunkCompare = lhs < rhs ? -1 : 1;
            int   indexCompare = indexInChunk - other.indexInChunk;
            return (lhs != rhs) ? chunkCompare : indexCompare;
        }

        public bool Equals(EntityLocationInChunk other)
        {
            return chunk.Equals(other.chunk) && indexInChunk.Equals(other.indexInChunk);
        }
    }

    public static unsafe class EntityManagerExposed
    {
        [BurstCompatible]
        public static EntityLocationInChunk GetEntityLocationInChunk(this EntityManager entityManager, Entity entity)
        {
            var ecs           = entityManager.GetCheckedEntityDataAccess()->EntityComponentStore;
            var entityInChunk = ecs->GetEntityInChunk(entity);
            return new EntityLocationInChunk
            {
                chunk        = new ArchetypeChunk(entityInChunk.Chunk, ecs),
                indexInChunk = entityInChunk.IndexInChunk
            };
        }

        [BurstCompatible]
        public static int GetIndexInChunk(this EntityManager entityManager, Entity entity)
        {
            var ecs           = entityManager.GetCheckedEntityDataAccess()->EntityComponentStore;
            var entityInChunk = ecs->GetEntityInChunk(entity);
            return entityInChunk.IndexInChunk;
        }

        [BurstCompatible]
        public static ComponentDataFromEntityExposed<T> GetExposedCDFE<T>(this EntityManager entityManager, bool isReadOnly) where T : struct, IComponentData
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            var ecs = entityManager.GetCheckedEntityDataAccess();
            //var ecs = entityManager.GetCheckedEntityDataAccess()->EntityComponentStore;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandles = &ecs->DependencyManager->Safety;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new ComponentDataFromEntityExposed<T>(typeIndex, ecs,
                safetyHandles->GetSafetyHandleForComponentDataFromEntity(typeIndex, isReadOnly));
#else
            return new ComponentDataFromEntityExposed<T>(typeIndex, ecs);
#endif
        }

        // [BurstCompatible]
        // public static EntityDataAccess* GetCheckedEntityDataAccess(this EntityManager entityManager)
        // {
        //     return entityManager.GetCheckedEntityDataAccess()->EntityComponentStore;
        // }
    }

    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public struct BufferTypeHandleFast<T>
        where T : struct, IBufferElementData
    {
        internal readonly int m_TypeIndex;
        internal readonly uint m_GlobalSystemVersion;
        internal readonly bool m_IsReadOnly;
        public short m_TypeLookupCache;
        internal LookupCache m_Cache;

        public uint GlobalSystemVersion => m_GlobalSystemVersion;
        public bool IsReadOnly => m_IsReadOnly;

#pragma warning disable 0414
        private readonly int m_Length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private readonly int m_MinIndex;
        private readonly int m_MaxIndex;

        internal AtomicSafetyHandle m_Safety0;
        internal AtomicSafetyHandle m_Safety1;
        internal int m_SafetyReadOnlyCount;
        internal int m_SafetyReadWriteCount;
#endif
#pragma warning restore 0414

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal BufferTypeHandleFast(AtomicSafetyHandle safety, AtomicSafetyHandle arrayInvalidationSafety, bool isReadOnly, uint globalSystemVersion)
#else
        internal BufferTypeHandleFast(bool isReadOnly, uint globalSystemVersion)
#endif
        {
            m_Length = 1;
            m_TypeIndex = TypeManager.GetTypeIndex<T>();
            m_GlobalSystemVersion = globalSystemVersion;
            m_IsReadOnly = isReadOnly;
            m_TypeLookupCache = -1;
            m_Cache = default;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_MinIndex = 0;
            m_MaxIndex = 0;
            m_Safety0 = safety;
            m_Safety1 = arrayInvalidationSafety;
            m_SafetyReadOnlyCount = isReadOnly ? 2 : 0;
            m_SafetyReadWriteCount = isReadOnly ? 0 : 2;
#endif
        }
    }

    public static unsafe class ChunkExtension
    {
        public static BufferAccessor<T> GetBufferAccessorFast<T>(this ArchetypeChunk chunk, ref BufferTypeHandleFast<T> bufferComponentTypeHandle)
            where T : struct, IBufferElementData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(bufferComponentTypeHandle.m_Safety0);
#endif
            var m_Chunk = chunk.m_Chunk;
            var archetype = m_Chunk->Archetype;
            var typeIndex = bufferComponentTypeHandle.m_TypeIndex;

            byte* ptr = (bufferComponentTypeHandle.IsReadOnly)
                ? ChunkDataUtility.GetComponentDataWithTypeRO(m_Chunk, archetype, 0, typeIndex, ref bufferComponentTypeHandle.m_Cache)
                : ChunkDataUtility.GetComponentDataWithTypeRW(m_Chunk, archetype, 0, typeIndex, bufferComponentTypeHandle.GlobalSystemVersion, ref bufferComponentTypeHandle.m_Cache);


//            ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex, ref bufferComponentTypeHandle.m_TypeLookupCache);
//            if (bufferComponentTypeHandle.m_TypeLookupCache == -1)
//            {
//#if ENABLE_UNITY_COLLECTIONS_CHECKS
//                return new BufferAccessor<T>(null, 0, 0, true, bufferComponentTypeHandle.m_Safety0, bufferComponentTypeHandle.m_Safety1, 0);
//#else
//                return new BufferAccessor<T>(null, 0, 0, 0);
//#endif
//            }

            int internalCapacity = archetype->BufferCapacities[bufferComponentTypeHandle.m_Cache.IndexInArcheType];

            //byte* ptr = (bufferComponentTypeHandle.IsReadOnly)
            //    ? ChunkDataUtility.GetComponentDataRO(m_Chunk, 0, bufferComponentTypeHandle.m_TypeLookupCache)
            //    : ChunkDataUtility.GetComponentDataRW(m_Chunk, 0, bufferComponentTypeHandle.m_TypeLookupCache, bufferComponentTypeHandle.GlobalSystemVersion);
            

            var length = chunk.Count;
            int stride = archetype->SizeOfs[bufferComponentTypeHandle.m_Cache.IndexInArcheType];
            var batchStartOffset = chunk.m_BatchStartEntityIndex * stride;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new BufferAccessor<T>(ptr + batchStartOffset, length, stride, bufferComponentTypeHandle.IsReadOnly, bufferComponentTypeHandle.m_Safety0, bufferComponentTypeHandle.m_Safety1, internalCapacity);
#else
            return new BufferAccessor<T>(ptr + batchStartOffset, length, stride, internalCapacity);
#endif
        }

        [BurstCompatible(GenericTypeArguments = new[] { typeof(BurstCompatibleBufferElement) })]
        public static BufferTypeHandleFast<T> GetBufferTypeHandleFast<T>(this EntityManager entityManager, bool isReadOnly)
            where T : struct, IBufferElementData
        {
            var ecs = entityManager.GetCheckedEntityDataAccess();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var typeIndex = TypeManager.GetTypeIndex<T>();
            return new BufferTypeHandleFast<T>(
                ecs->DependencyManager->Safety.GetSafetyHandleForBufferTypeHandle(typeIndex, isReadOnly),
                ecs->DependencyManager->Safety.GetBufferHandleForBufferTypeHandle(typeIndex),
                isReadOnly, ecs->EntityComponentStore->GlobalSystemVersion);
#else
            return new BufferTypeHandleFast<T>(isReadOnly, ecs->EntityComponentStore->GlobalSystemVersion);
#endif
        }
    }
}

