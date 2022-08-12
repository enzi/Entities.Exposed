using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Exposed
{
    /// <summary>
    /// A [NativeContainer] that provides access to all instances of components of type T, indexed by <see cref="Entity"/>.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IComponentData"/> to access.</typeparam>
    /// <remarks>
    /// ComponentDataFromEntity is a native container that provides array-like access to components of a specific
    /// type. You can use ComponentDataFromEntity to look up data associated with one entity while iterating over a
    /// different set of entities. For example, Unity.Transforms stores the <see cref="Entity"/> object of parent entities
    /// in a Parent component and looks up the parent's LocalToWorld matrix using
    /// ComponentDataFromEntity&lt;LocalToWorld&gt; when calculating the world positions of child entities.
    ///
    /// To get a ComponentDataFromEntity, call <see cref="ComponentSystemBase.GetComponentDataFromEntity"/>.
    ///
    /// Pass a ComponentDataFromEntity container to a job by defining a public field of the appropriate type
    /// in your IJob implementation. You can safely read from ComponentDataFromEntity in any job, but by
    /// default, you cannot write to components in the container in parallel jobs (including
    /// <see cref="IJobForEach{T0}"/> and <see cref="IJobChunk"/>). If you know that two instances of a parallel
    /// job can never write to the same index in the container, you can disable the restriction on parallel writing
    /// by adding [NativeDisableParallelForRestrictionAttribute] to the ComponentDataFromEntity field definition in the job struct.
    ///
    /// If you would like to access an entity's components outside of a job, consider using the <see cref="EntityManager"/> methods
    /// <see cref="EntityManager.GetComponentData"/> and <see cref="EntityManager.SetComponentData"/>
    /// instead, to avoid the overhead of creating a ComponentDataFromEntity object.
    ///
    /// [NativeContainer]: https://docs.unity3d.com/ScriptReference/Unity.Collections.LowLevel.Unsafe.NativeContainerAttribute
    /// [NativeDisableParallelForRestrictionAttribute]: https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeDisableParallelForRestrictionAttribute.html
    /// </remarks>
    [NativeContainer]
    public unsafe struct ComponentDataFromEntityExposed<T> where T : unmanaged, IComponentData
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        readonly AtomicSafetyHandle      m_Safety;
#endif
        [NativeDisableUnsafePtrRestriction]
        readonly EntityDataAccess*       m_Access;
        readonly int                     m_TypeIndex;
        readonly uint                    m_GlobalSystemVersion;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        readonly bool                    m_IsZeroSized;          // cache of whether T is zero-sized
#endif
        LookupCache                      m_Cache;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal ComponentDataFromEntityExposed(int typeIndex, EntityDataAccess* access, AtomicSafetyHandle safety)
        {
            m_Safety = safety;
            m_TypeIndex = typeIndex;
            m_Access = access;
            m_Cache = default;
            m_GlobalSystemVersion = access->EntityComponentStore->GlobalSystemVersion;
            m_IsZeroSized = ComponentType.FromTypeIndex(typeIndex).IsZeroSized;
        }

#else
        internal ComponentDataFromEntityExposed(int typeIndex, EntityDataAccess* access)
        {
            m_TypeIndex = typeIndex;
            m_Access = access;
            m_Cache = default;
            m_GlobalSystemVersion = access->EntityComponentStore->GlobalSystemVersion;
        }

#endif

        public bool Exists(Entity entity)
        {
            var ecs = m_Access->EntityComponentStore;
            return ecs->Exists(entity);
        }
        
        /// <summary>
        /// Gets the <see cref="IComponentData"/> instance of type T for the specified entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>An <see cref="IComponentData"/> type.</returns>
        /// <remarks>You cannot use ComponentDataFromEntity to get zero-sized <see cref="IComponentData"/>.
        /// Use <see cref="HasComponent"/> to check whether an entity has the zero-sized component instead.
        ///
        /// Normally, you cannot write to components accessed using a ComponentDataFromEntity instance
        /// in a parallel Job. This restriction is in place because multiple threads could write to the same component,
        /// leading to a race condition and nondeterministic results. However, when you are certain that your algorithm
        /// cannot write to the same component from different threads, you can manually disable this safety check
        /// by putting the [NativeDisableParallelForRestrictions] attribute on the ComponentDataFromEntity field in the Job.
        ///
        /// [NativeDisableParallelForRestrictionAttribute]: https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeDisableParallelForRestrictionAttribute.html
        /// </remarks>
        /// <exception cref="System.ArgumentException">Thrown if T is zero-size.</exception>
        public T this[Entity entity]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
				var ecs = m_Access->EntityComponentStore;
                ecs->AssertEntityHasComponent(entity, m_TypeIndex);

                CheckComponentIsZeroSized();

                void* ptr = ecs->GetComponentDataWithTypeRO(entity, m_TypeIndex, ref m_Cache);
                UnsafeUtility.CopyPtrToStructure(ptr, out T data);

                return data;
            }
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
				var ecs = m_Access->EntityComponentStore;
                ecs->AssertEntityHasComponent(entity, m_TypeIndex);

                CheckComponentIsZeroSized();

                void* ptr = ecs->GetComponentDataWithTypeRW(entity, m_TypeIndex, m_GlobalSystemVersion, ref m_Cache);
                UnsafeUtility.CopyStructureToPtr(ref value, ptr);
            }
        }

        /// <summary>
        /// Reports whether the specified <see cref="Entity"/> instance still refers to a valid entity and that it has a
        /// component of type T.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>True if the entity has a component of type T, and false if it does not. Also returns false if
        /// the Entity instance refers to an entity that has been destroyed.</returns>
        /// <remarks>To report if the provided entity has a component of type T, this function confirms
        /// whether the <see cref="EntityArchetype"/> of the provided entity includes components of type T.
        /// </remarks>
        public bool HasComponent(Entity entity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
			var ecs = m_Access->EntityComponentStore;
			return ecs->HasComponent(entity, m_TypeIndex, ref m_Cache);
            //return m_EntityComponentStore->HasComponent(entity, m_TypeIndex);
        }
		
		/// <summary>
        /// Retrieves the component associated with the specified <see cref="Entity"/>, if it exists. Then reports if the instance still refers to a valid entity and that it has a
        /// component of type T.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// /// <param name="componentData">The component of type T for the given entity, if it exists.</param>
        /// <returns>True if the entity has a component of type T, and false if it does not.</returns>
        /// <remarks>To report if the provided entity has a component of type T, this function confirms
        /// whether the <see cref="EntityArchetype"/> of the provided entity includes components of type T.
        /// </remarks>
		public bool TryGetComponent(Entity entity, out T componentData)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            CheckComponentIsZeroSized();

            var ecs = m_Access->EntityComponentStore;

            var hasComponent = ecs->HasComponent(entity, m_TypeIndex, ref m_Cache);
            if (hasComponent)
            {
                void* ptr = ecs->GetComponentDataWithTypeRO(entity, m_TypeIndex, ref m_Cache);
                UnsafeUtility.CopyPtrToStructure(ptr, out componentData);
            }
            else
            {
                componentData = default;
                return false;
            }

            return true;
        }
        
		public bool TryGetComponentPtrRO(Entity entity, out T* ptr)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            CheckComponentIsZeroSized();

            var ecs = m_Access->EntityComponentStore;

            var hasComponent = ecs->HasComponent(entity, m_TypeIndex, ref m_Cache);
            if (hasComponent)
            {
                ptr = (T*) ecs->GetComponentDataWithTypeRO(entity, m_TypeIndex, ref m_Cache);                
            }
            else
            {
                ptr = null;
                return false;
            }

            return true;
        }

        public void SetChangeVersion(Entity entity)
        {
            var ecs = m_Access->EntityComponentStore;
            var chunk = ecs->GetChunk(entity);

            if (m_Cache.IndexInArcheType == -1)
            {
                int IndexInArcheType = ChunkDataUtility.GetIndexInTypeArray(chunk->Archetype, m_TypeIndex);
                chunk->SetChangeVersion(IndexInArcheType, m_GlobalSystemVersion);
            }
            else
                chunk->SetChangeVersion(m_Cache.IndexInArcheType, m_GlobalSystemVersion);
        }
		
// 		public bool TryGetComponentRefRW(Entity entity, out void* ptr)
//         {
// #if ENABLE_UNITY_COLLECTIONS_CHECKS
//             AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
// #endif
//             CheckComponentIsZeroSized();
//
//             var ecs = m_Access->EntityComponentStore;
//
//             var hasComponent = ecs->HasComponent(entity, m_TypeIndex, ref m_Cache);
//             if (hasComponent)
//             {
//                 ptr = ecs->GetComponentDataWithTypeRO(entity, m_TypeIndex, ref m_Cache);                
//             }
//             else
//             {
//                 ptr = null;
//                 return false;
//             }
//
//             return true;
//         }
        
        public bool TryGetComponentPtrRW(Entity entity, out T* ptr)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            CheckComponentIsZeroSized();

            var ecs = m_Access->EntityComponentStore;

            var hasComponent = ecs->HasComponent(entity, m_TypeIndex, ref m_Cache);
            if (hasComponent)
            {
                ptr = (T*) ecs->GetComponentDataWithTypeRO(entity, m_TypeIndex, ref m_Cache);                
            }
            else
            {
                ptr = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Reports whether any of IComponentData components of the type T, in the chunk containing the
        /// specified <see cref="Entity"/>, could have changed.
        /// </summary>
        /// <remarks>
        /// Note that for efficiency, the change version applies to whole chunks not individual entities. The change
        /// version is incremented even when another job or system that has declared write access to a component does
        /// not actually change the component value.</remarks>
        /// <param name="entity">The entity.</param>
        /// <param name="version">The version to compare. In a system, this parameter should be set to the
        /// current <see cref="Unity.Entities.ComponentSystemBase.LastSystemVersion"/> at the time the job is run or
        /// scheduled.</param>
        /// <returns>True, if the version number stored in the chunk for this component is more recent than the version
        /// passed to the <paramref name="version"/> parameter.</returns>
        public bool DidChange(Entity entity, uint version)
        {
			var ecs = m_Access->EntityComponentStore;
            var chunk = ecs->GetChunk(entity);

            var typeIndexInArchetype = ChunkDataUtility.GetIndexInTypeArray(chunk->Archetype, m_TypeIndex);
            if (typeIndexInArchetype == -1) return false;
            var chunkVersion = chunk->GetChangeVersion(typeIndexInArchetype);

            return ChangeVersionUtility.DidChange(chunkVersion, version);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckComponentIsZeroSized()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_IsZeroSized)
                throw new System.ArgumentException($"ComponentDataFromEntity<{typeof(T)}> indexer can not index the component because it is zero sized, you can use Exists instead.");
#endif
        }

        public void* GetPtr(Entity entity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            var ecs = m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(entity, m_TypeIndex);

            CheckComponentIsZeroSized();

            void* ptr = ecs->GetComponentDataWithTypeRW(entity, m_TypeIndex, m_GlobalSystemVersion, ref m_Cache);
            return ptr;
        }

        public void* GetPtrRO(Entity entity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            var ecs = m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(entity, m_TypeIndex);

            CheckComponentIsZeroSized();

            void* ptr = ecs->GetComponentDataWithTypeRO(entity, m_TypeIndex, ref m_Cache);
            return ptr;
        }

        public ref T GetRef(Entity entity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            var ecs = m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(entity, m_TypeIndex);

            CheckComponentIsZeroSized();

            void* ptr = ecs->GetComponentDataWithTypeRW(entity, m_TypeIndex, m_GlobalSystemVersion, ref m_Cache);
            return ref UnsafeUtility.AsRef<T>(ptr);
        }

        public ref T GetRefRO(Entity entity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            var ecs = m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(entity, m_TypeIndex);

            CheckComponentIsZeroSized();

            void* ptr = ecs->GetComponentDataWithTypeRO(entity, m_TypeIndex, ref m_Cache);
            return ref UnsafeUtility.AsRef<T>(ptr);
        }

        internal byte* GetComponentDataWithTypeRO(Chunk* chunk, Archetype* archetype, int indexInChunk, int typeIndex)
        {
            //return ChunkDataUtility.GetComponentDataWithTypeRO(chunk, archetype, indexInChunk, typeIndex, ref m_Cache);
            
            if (m_Cache.Archetype != archetype)
            {
                m_Cache.Archetype = archetype;
                ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex, ref m_Cache.IndexInArcheType);
                m_Cache.ComponentOffset = archetype->Offsets[m_Cache.IndexInArcheType];
                m_Cache.ComponentSizeOf = archetype->SizeOfs[m_Cache.IndexInArcheType];
            }

            return chunk->Buffer + (m_Cache.ComponentOffset + m_Cache.ComponentSizeOf * indexInChunk);
        }

        internal byte* GetComponentDataWithTypeRW(Chunk* chunk, Archetype* archetype, int indexInChunk, int typeIndex, uint globalVersion)
        {
            //return ChunkDataUtility.GetComponentDataWithTypeRW(chunk, archetype, indexInChunk, typeIndex, globalVersion, ref m_Cache);
            if (m_Cache.Archetype != archetype)
            {
                m_Cache.Archetype = archetype;
                ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex, ref m_Cache.IndexInArcheType);
                m_Cache.ComponentOffset = archetype->Offsets[m_Cache.IndexInArcheType];
                m_Cache.ComponentSizeOf = archetype->SizeOfs[m_Cache.IndexInArcheType];
            }

            // Write Component to Chunk. ChangeVersion:Yes OrderVersion:No
            chunk->SetChangeVersion(m_Cache.IndexInArcheType, globalVersion);
            return chunk->Buffer + (m_Cache.ComponentOffset + m_Cache.ComponentSizeOf * indexInChunk);
        }
        
        // internal bool HasComponentFast(Entity entity, int type, ref LookupCache cache)
        // {
        //     if (!Exists(entity))
        //         return false;
        //
        //     var archetype = m_ArchetypeByEntity[entity.Index];
        //     if (Hint.Unlikely(archetype != cache.Archetype))
        //     {
        //         cache.Archetype = archetype;
        //         ChunkDataUtility.GetIndexInTypeArray(archetype, type, ref cache.IndexInArcheType);
        //         if (Hint.Likely(cache.IndexInArcheType != -1))
        //         {
        //             cache.ComponentOffset = archetype->Offsets[cache.IndexInArcheType];
        //             cache.ComponentSizeOf = archetype->SizeOfs[cache.IndexInArcheType];
        //         }
        //     }
        //     return cache.IndexInArcheType != -1;
        // }
        
        public T SimpleGet(Entity entity, EntityStorageInfo si)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            var ecs = m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(entity, m_TypeIndex);

            CheckComponentIsZeroSized();

            void* ptr = GetComponentDataWithTypeRO(si.Chunk.m_Chunk, si.Chunk.Archetype.Archetype, si.IndexInChunk, m_TypeIndex);
            UnsafeUtility.CopyPtrToStructure(ptr, out T data);

            return data;
        }
        
        public bool TryGetComponentFast(Entity entity, EntityStorageInfo si, out T componentData)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            CheckComponentIsZeroSized();

            var ecs = m_Access->EntityComponentStore;

            var hasComponent = ecs->HasComponent(entity, m_TypeIndex, ref m_Cache);
            if (hasComponent)
            {
                void* ptr = GetComponentDataWithTypeRO(si.Chunk.m_Chunk, si.Chunk.Archetype.Archetype, si.IndexInChunk, m_TypeIndex);
                UnsafeUtility.CopyPtrToStructure(ptr, out componentData);
            }
            else
            {
                componentData = default;
                return false;
            }

            return true;
        }

        public bool TryGetComponentPtrROFast(Entity entity, EntityStorageInfo si, out T* ptr)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            CheckComponentIsZeroSized();

            var ecs = m_Access->EntityComponentStore;

            var hasComponent = ecs->HasComponent(entity, m_TypeIndex, ref m_Cache);
            if (hasComponent)
            {
                //ptr = (T*) ecs->GetComponentDataWithTypeRO(entity, m_TypeIndex, ref m_Cache);                
                ptr = (T*)GetComponentDataWithTypeRO(si.Chunk.m_Chunk, si.Chunk.Archetype.Archetype, si.IndexInChunk, m_TypeIndex);
            }
            else
            {
                ptr = null;
                return false;
            }

            return true;
        }
		
		public bool IsComponentEnabled(Entity entity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return m_Access->IsComponentEnabled(entity, m_TypeIndex);
        }

        public void SetComponentEnabled(Entity entity, bool value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            m_Access->SetComponentEnabled(entity, m_TypeIndex, value);
        }
    }
}
