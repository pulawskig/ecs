// ----------------------------------------------------------------------------
// The MIT License
// Lightweight ECS framework https://github.com/Byteron/ecs
// based on https://github.com/Leopotam/ecslite
// Copyright (c) 2021 Aaron Winter <winter.aaron93@gmail.com>
// Copyright (c) 2021 Leopotam <leopotam@gmail.com>
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Bitron.Ecs
{
    public sealed class EcsWorld
    {
        internal EntityData[] Entities;
        int _entitiesCount;
        int[] _recycledEntities;
        int _recycledEntitiesCount;
        IEcsPool[] _pools;
        int _poolsCount;
        readonly int _poolDenseSize;
        readonly Dictionary<Type, IEcsPool> _poolHashes;
        readonly Dictionary<int, EcsQuery> _hashedQuerys;
        readonly List<EcsQuery> _allQuerys;
        List<EcsQuery>[] _queriesByIncludedComponents;
        List<EcsQuery>[] _queriesByExcludedComponents;
        Dictionary<Type, object> _resources;
        bool _destroyed;
#if DEBUG || LEOECSLITE_WORLD_EVENTS
        List<IEcsWorldEventListener> _eventListeners;

        public void AddEventListener(IEcsWorldEventListener listener)
        {
#if DEBUG
            if (listener == null) { throw new Exception("Listener is null."); }
#endif
            _eventListeners.Add(listener);
        }

        public void RemoveEventListener(IEcsWorldEventListener listener)
        {
#if DEBUG
            if (listener == null) { throw new Exception("Listener is null."); }
#endif
            _eventListeners.Remove(listener);
        }

        public void RaiseEntityChangeEvent(int entity)
        {
            for (int ii = 0, iMax = _eventListeners.Count; ii < iMax; ii++)
            {
                _eventListeners[ii].OnEntityChanged(entity);
            }
        }
#endif
#if DEBUG
        readonly List<int> _leakedEntities = new List<int>(512);

        internal bool CheckForLeakedEntities()
        {
            if (_leakedEntities.Count > 0)
            {
                for (int i = 0, iMax = _leakedEntities.Count; i < iMax; i++)
                {
                    ref var entityData = ref Entities[_leakedEntities[i]];
                    if (entityData.Gen > 0 && entityData.ComponentsCount == 0)
                    {
                        return true;
                    }
                }
                _leakedEntities.Clear();
            }
            return false;
        }
#endif

        public EcsWorld(in Config cfg = default)
        {
            // entities.
            var capacity = cfg.Entities > 0 ? cfg.Entities : Config.EntitiesDefault;
            Entities = new EntityData[capacity];
            capacity = cfg.RecycledEntities > 0 ? cfg.RecycledEntities : Config.RecycledEntitiesDefault;
            _recycledEntities = new int[capacity];
            _entitiesCount = 0;
            _recycledEntitiesCount = 0;
            // pools.
            capacity = cfg.Pools > 0 ? cfg.Pools : Config.PoolsDefault;
            _pools = new IEcsPool[capacity];
            _poolHashes = new Dictionary<Type, IEcsPool>(capacity);
            _queriesByIncludedComponents = new List<EcsQuery>[capacity];
            _queriesByExcludedComponents = new List<EcsQuery>[capacity];
            _poolsCount = 0;
            // queries.
            capacity = cfg.Querys > 0 ? cfg.Querys : Config.QuerysDefault;
            _hashedQuerys = new Dictionary<int, EcsQuery>(capacity);
            _allQuerys = new List<EcsQuery>(capacity);
            _poolDenseSize = cfg.PoolDenseSize > 0 ? cfg.PoolDenseSize : Config.PoolDenseSizeDefault;
            // resources
            _resources = new Dictionary<Type, object>();
#if DEBUG || LEOECSLITE_WORLD_EVENTS
            _eventListeners = new List<IEcsWorldEventListener>(4);
#endif
            _destroyed = false;
        }

        public void Destroy()
        {
#if DEBUG
            if (CheckForLeakedEntities()) { throw new Exception($"Empty entity detected before EcsWorld.Destroy()."); }
#endif
            _destroyed = true;
            for (var i = _entitiesCount - 1; i >= 0; i--)
            {
                ref var entityData = ref Entities[i];
                if (entityData.ComponentsCount > 0)
                {
                    DestroyEntity(i);
                }
            }
            _pools = Array.Empty<IEcsPool>();
            _poolHashes.Clear();
            _hashedQuerys.Clear();
            _allQuerys.Clear();
            _queriesByIncludedComponents = Array.Empty<List<EcsQuery>>();
            _queriesByExcludedComponents = Array.Empty<List<EcsQuery>>();
            _resources.Clear();
#if DEBUG || LEOECSLITE_WORLD_EVENTS
            for (var ii = _eventListeners.Count - 1; ii >= 0; ii--)
            {
                _eventListeners[ii].OnWorldDestroyed(this);
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAlive()
        {
            return !_destroyed;
        }

        public int CreateEntity()
        {
            int entity;
            if (_recycledEntitiesCount > 0)
            {
                entity = _recycledEntities[--_recycledEntitiesCount];
                ref var entityData = ref Entities[entity];
                entityData.Gen = (short)-entityData.Gen;
            }
            else
            {
                // new entity.
                if (_entitiesCount == Entities.Length)
                {
                    // resize entities and component pools.
                    var newSize = _entitiesCount << 1;
                    Array.Resize(ref Entities, newSize);
                    for (int i = 0, iMax = _poolsCount; i < iMax; i++)
                    {
                        _pools[i].Resize(newSize);
                    }
                    for (int i = 0, iMax = _allQuerys.Count; i < iMax; i++)
                    {
                        _allQuerys[i].ResizeSparseIndex(newSize);
                    }
#if DEBUG || LEOECSLITE_WORLD_EVENTS
                    for (int ii = 0, iMax = _eventListeners.Count; ii < iMax; ii++)
                    {
                        _eventListeners[ii].OnWorldResized(newSize);
                    }
#endif
                }
                entity = _entitiesCount++;
                Entities[entity].Gen = 1;
            }
#if DEBUG
            _leakedEntities.Add(entity);
#endif
#if DEBUG || LEOECSLITE_WORLD_EVENTS
            for (int ii = 0, iMax = _eventListeners.Count; ii < iMax; ii++)
            {
                _eventListeners[ii].OnEntityCreated(entity);
            }
#endif
            return entity;
        }

        public void DestroyEntity(int entity)
        {
#if DEBUG
            if (entity < 0 || entity >= _entitiesCount)
            {
                throw new Exception("Cant touch destroyed entity.");
            }
#endif
            ref var entityData = ref Entities[entity];
            if (entityData.Gen < 0)
            {
                return;
            }
            // kill components.
            if (entityData.ComponentsCount > 0)
            {
                var idx = 0;
                while (entityData.ComponentsCount > 0 && idx < _poolsCount)
                {
                    for (; idx < _poolsCount; idx++)
                    {
                        if (_pools[idx].Has(entity))
                        {
                            _pools[idx++].Del(entity);
                            break;
                        }
                    }
                }
#if DEBUG
                if (entityData.ComponentsCount != 0) { throw new Exception($"Invalid components count on entity {entity} => {entityData.ComponentsCount}."); }
#endif
                return;
            }
            entityData.Gen = (short)(entityData.Gen == short.MaxValue ? -1 : -(entityData.Gen + 1));
            if (_recycledEntitiesCount == _recycledEntities.Length)
            {
                Array.Resize(ref _recycledEntities, _recycledEntitiesCount << 1);
            }
            _recycledEntities[_recycledEntitiesCount++] = entity;
#if DEBUG || LEOECSLITE_WORLD_EVENTS
            for (int ii = 0, iMax = _eventListeners.Count; ii < iMax; ii++)
            {
                _eventListeners[ii].OnEntityDestroyed(entity);
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetComponentsCount(int entity)
        {
            return Entities[entity].ComponentsCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short GetEntityGen(int entity)
        {
            return Entities[entity].Gen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetAllocatedEntitiesCount()
        {
            return _entitiesCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetWorldSize()
        {
            return Entities.Length;
        }

        public EcsPool<T> GetPool<T>() where T : struct
        {
            var poolType = typeof(T);
            if (_poolHashes.TryGetValue(poolType, out var rawPool))
            {
                return (EcsPool<T>)rawPool;
            }
            var pool = new EcsPool<T>(this, _poolsCount, _poolDenseSize, Entities.Length);
            _poolHashes[poolType] = pool;
            if (_poolsCount == _pools.Length)
            {
                var newSize = _poolsCount << 1;
                Array.Resize(ref _pools, newSize);
                Array.Resize(ref _queriesByIncludedComponents, newSize);
                Array.Resize(ref _queriesByExcludedComponents, newSize);
            }
            _pools[_poolsCount++] = pool;
            return pool;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEcsPool GetPoolById(int typeId)
        {
            return typeId >= 0 && typeId < _poolsCount ? _pools[typeId] : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEcsPool GetPoolByType(Type type)
        {
            return _poolHashes.TryGetValue(type, out var pool) ? pool : null;
        }

        public int GetAllEntities(ref int[] entities)
        {
            var count = _entitiesCount - _recycledEntitiesCount;
            if (entities == null || entities.Length < count)
            {
                entities = new int[count];
            }
            var id = 0;
            for (int i = 0, iMax = _entitiesCount; i < iMax; i++)
            {
                ref var entityData = ref Entities[i];
                // should we skip empty entities here?
                if (entityData.Gen > 0 && entityData.ComponentsCount >= 0)
                {
                    entities[id++] = i;
                }
            }
            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EcsQuery.Mask Query<T>() where T : struct
        {
            return EcsQuery.Mask.New(this).Inc<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddResource<T>(T resource) where T : struct
        {
            _resources.Add(typeof(T), resource);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetResource<T>() where T : struct
        {
            return (T)_resources[typeof(T)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveResource<T>() where T : struct
        {
            _resources.Remove(typeof(T));
        }

        public int GetComponents(int entity, ref object[] list)
        {
            var itemsCount = Entities[entity].ComponentsCount;
            if (itemsCount == 0) { return 0; }
            if (list == null || list.Length < itemsCount)
            {
                list = new object[_pools.Length];
            }
            for (int i = 0, j = 0, iMax = _poolsCount; i < iMax; i++)
            {
                if (_pools[i].Has(entity))
                {
                    list[j++] = _pools[i].GetRaw(entity);
                }
            }
            return itemsCount;
        }

        public int GetComponentTypes(int entity, ref Type[] list)
        {
            var itemsCount = Entities[entity].ComponentsCount;
            if (itemsCount == 0) { return 0; }
            if (list == null || list.Length < itemsCount)
            {
                list = new Type[_pools.Length];
            }
            for (int i = 0, j = 0, iMax = _poolsCount; i < iMax; i++)
            {
                if (_pools[i].Has(entity))
                {
                    list[j++] = _pools[i].GetComponentType();
                }
            }
            return itemsCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsEntityAliveInternal(int entity)
        {
            return entity >= 0 && entity < _entitiesCount && Entities[entity].Gen > 0;
        }

        internal (EcsQuery, bool) GetQueryInternal(EcsQuery.Mask mask, int capacity = 512)
        {
            var hash = mask.Hash;
            var exists = _hashedQuerys.TryGetValue(hash, out var query);
            if (exists) { return (query, false); }
            query = new EcsQuery(this, mask, capacity, Entities.Length);
            _hashedQuerys[hash] = query;
            _allQuerys.Add(query);
            // add to component dictionaries for fast compatibility scan.
            for (int i = 0, iMax = mask.IncludeCount; i < iMax; i++)
            {
                var list = _queriesByIncludedComponents[mask.Include[i]];
                if (list == null)
                {
                    list = new List<EcsQuery>(8);
                    _queriesByIncludedComponents[mask.Include[i]] = list;
                }
                list.Add(query);
            }
            for (int i = 0, iMax = mask.ExcludeCount; i < iMax; i++)
            {
                var list = _queriesByExcludedComponents[mask.Exclude[i]];
                if (list == null)
                {
                    list = new List<EcsQuery>(8);
                    _queriesByExcludedComponents[mask.Exclude[i]] = list;
                }
                list.Add(query);
            }
            // scan exist entities for compatibility with new query.
            for (int i = 0, iMax = _entitiesCount; i < iMax; i++)
            {
                ref var entityData = ref Entities[i];
                if (entityData.ComponentsCount > 0 && IsMaskCompatible(mask, i))
                {
                    query.AddEntity(i);
                }
            }
#if DEBUG || LEOECSLITE_WORLD_EVENTS
            for (int ii = 0, iMax = _eventListeners.Count; ii < iMax; ii++)
            {
                _eventListeners[ii].OnQueryCreated(query);
            }
#endif
            return (query, true);
        }

        internal void OnEntityChange(int entity, int componentType, bool added)
        {
            var includeList = _queriesByIncludedComponents[componentType];
            var excludeList = _queriesByExcludedComponents[componentType];
            if (added)
            {
                // add component.
                if (includeList != null)
                {
                    foreach (var query in includeList)
                    {
                        if (IsMaskCompatible(query.GetMask(), entity))
                        {
#if DEBUG
                            if (query.SparseEntities[entity] > 0) { throw new Exception("Entity already in query."); }
#endif
                            query.AddEntity(entity);
                        }
                    }
                }
                if (excludeList != null)
                {
                    foreach (var query in excludeList)
                    {
                        if (IsMaskCompatibleWithout(query.GetMask(), entity, componentType))
                        {
#if DEBUG
                            if (query.SparseEntities[entity] == 0) { throw new Exception("Entity not in query."); }
#endif
                            query.RemoveEntity(entity);
                        }
                    }
                }
            }
            else
            {
                // remove component.
                if (includeList != null)
                {
                    foreach (var query in includeList)
                    {
                        if (IsMaskCompatible(query.GetMask(), entity))
                        {
#if DEBUG
                            if (query.SparseEntities[entity] == 0) { throw new Exception("Entity not in query."); }
#endif
                            query.RemoveEntity(entity);
                        }
                    }
                }
                if (excludeList != null)
                {
                    foreach (var query in excludeList)
                    {
                        if (IsMaskCompatibleWithout(query.GetMask(), entity, componentType))
                        {
#if DEBUG
                            if (query.SparseEntities[entity] > 0) { throw new Exception("Entity already in query."); }
#endif
                            query.AddEntity(entity);
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsMaskCompatible(EcsQuery.Mask queryMask, int entity)
        {
            for (int i = 0, iMax = queryMask.IncludeCount; i < iMax; i++)
            {
                if (!_pools[queryMask.Include[i]].Has(entity))
                {
                    return false;
                }
            }
            for (int i = 0, iMax = queryMask.ExcludeCount; i < iMax; i++)
            {
                if (_pools[queryMask.Exclude[i]].Has(entity))
                {
                    return false;
                }
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsMaskCompatibleWithout(EcsQuery.Mask queryMask, int entity, int componentId)
        {
            for (int i = 0, iMax = queryMask.IncludeCount; i < iMax; i++)
            {
                var typeId = queryMask.Include[i];
                if (typeId == componentId || !_pools[typeId].Has(entity))
                {
                    return false;
                }
            }
            for (int i = 0, iMax = queryMask.ExcludeCount; i < iMax; i++)
            {
                var typeId = queryMask.Exclude[i];
                if (typeId != componentId && _pools[typeId].Has(entity))
                {
                    return false;
                }
            }
            return true;
        }

        public struct Config
        {
            public int Entities;
            public int RecycledEntities;
            public int Pools;
            public int Querys;
            public int PoolDenseSize;

            internal const int EntitiesDefault = 512;
            internal const int RecycledEntitiesDefault = 512;
            internal const int PoolsDefault = 512;
            internal const int QuerysDefault = 512;
            internal const int PoolDenseSizeDefault = 512;
        }

        internal struct EntityData
        {
            public short Gen;
            public short ComponentsCount;
        }
    }

#if DEBUG || LEOECSLITE_WORLD_EVENTS
    public interface IEcsWorldEventListener
    {
        void OnEntityCreated(int entity);
        void OnEntityChanged(int entity);
        void OnEntityDestroyed(int entity);
        void OnQueryCreated(EcsQuery query);
        void OnWorldResized(int newSize);
        void OnWorldDestroyed(EcsWorld world);
    }
#endif
}
