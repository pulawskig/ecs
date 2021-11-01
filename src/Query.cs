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
#if LEOECSLITE_FILTER_EVENTS
    public interface IEcsQueryEventListener {
        void OnEntityAdded (int entity);
        void OnEntityRemoved (int entity);
    }
#endif
    public sealed class EcsQuery
    {
        readonly EcsWorld _world;
        readonly Mask _mask;

        int[] _denseEntities;
        int _entitiesCount;
        internal int[] SparseEntities;
        int _lockCount;
        DelayedOp[] _delayedOps;
        int _delayedOpsCount;
        Dictionary<Type, IEcsPool> _pools;

#if LEOECSLITE_FILTER_EVENTS
        IEcsQueryEventListener[] _eventListeners = new IEcsQueryEventListener[4];
        int _eventListenersCount;
#endif

        internal EcsQuery(EcsWorld world, Mask mask, int denseCapacity, int sparseCapacity)
        {
            _world = world;
            _mask = mask;
            _denseEntities = new int[denseCapacity];
            SparseEntities = new int[sparseCapacity];
            _entitiesCount = 0;
            _delayedOps = new DelayedOp[512];
            _delayedOpsCount = 0;
            _lockCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EcsWorld GetWorld()
        {
            return _world;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEntitiesCount()
        {
            return _entitiesCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int[] GetRawEntities()
        {
            return _denseEntities;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int[] GetSparseIndex()
        {
            return SparseEntities;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator()
        {
            _lockCount++;
            return new Enumerator(this);
        }

#if LEOECSLITE_FILTER_EVENTS
        public void AddEventListener (IEcsQueryEventListener eventListener) {
#if DEBUG
            for (var i = 0; i < _eventListenersCount; i++) {
                if (_eventListeners[i] == eventListener) {
                    throw new Exception ("Listener already subscribed.");
                }
            }
#endif
            if (_eventListeners.Length == _eventListenersCount) {
                Array.Resize (ref _eventListeners, _eventListenersCount << 1);
            }
            _eventListeners[_eventListenersCount++] = eventListener;
        }

        public void RemoveEventListener (IEcsQueryEventListener eventListener) {
            for (var i = 0; i < _eventListenersCount; i++) {
                if (_eventListeners[i] == eventListener) {
                    _eventListenersCount--;
                    // cant fill gap with last element due listeners order is important.
                    Array.Copy (_eventListeners, i + 1, _eventListeners, i, _eventListenersCount - i);
                    break;
                }
            }
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ResizeSparseIndex(int capacity)
        {
            Array.Resize(ref SparseEntities, capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Mask GetMask()
        {
            return _mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddEntity(int entity)
        {
            if (AddDelayedOp(true, entity)) { return; }
            if (_entitiesCount == _denseEntities.Length)
            {
                Array.Resize(ref _denseEntities, _entitiesCount << 1);
            }
            _denseEntities[_entitiesCount++] = entity;
            SparseEntities[entity] = _entitiesCount;
#if LEOECSLITE_FILTER_EVENTS
            ProcessEventListeners (true, entity);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveEntity(int entity)
        {
            if (AddDelayedOp(false, entity)) { return; }
            var idx = SparseEntities[entity] - 1;
            SparseEntities[entity] = 0;
            _entitiesCount--;
            if (idx < _entitiesCount)
            {
                _denseEntities[idx] = _denseEntities[_entitiesCount];
                SparseEntities[_denseEntities[idx]] = idx + 1;
            }
#if LEOECSLITE_FILTER_EVENTS
            ProcessEventListeners (false, entity);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool AddDelayedOp(bool added, int entity)
        {
            if (_lockCount <= 0) { return false; }
            if (_delayedOpsCount == _delayedOps.Length)
            {
                Array.Resize(ref _delayedOps, _delayedOpsCount << 1);
            }
            ref var op = ref _delayedOps[_delayedOpsCount++];
            op.Added = added;
            op.Entity = entity;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Unlock()
        {
#if DEBUG
            if (_lockCount <= 0)
            {
                throw new Exception($"Invalid lock-unlock balance for \"{GetType().Name}\".");
            }
#endif
            _lockCount--;
            if (_lockCount == 0 && _delayedOpsCount > 0)
            {
                for (int i = 0, iMax = _delayedOpsCount; i < iMax; i++)
                {
                    ref var op = ref _delayedOps[i];
                    if (op.Added)
                    {
                        AddEntity(op.Entity);
                    }
                    else
                    {
                        RemoveEntity(op.Entity);
                    }
                }
                _delayedOpsCount = 0;
            }
        }

#if LEOECSLITE_FILTER_EVENTS
        void ProcessEventListeners (bool isAdd, int entity) {
            if (isAdd) {
                for (var i = 0; i < _eventListenersCount; i++) {
                    _eventListeners[i].OnEntityAdded (entity);
                }
            } else {
                for (var i = 0; i < _eventListenersCount; i++) {
                    _eventListeners[i].OnEntityRemoved (entity);
                }
            }
        }
#endif

        public struct Enumerator : IDisposable
        {
            readonly EcsQuery _query;
            readonly int[] _entities;
            readonly int _count;
            int _idx;

            public Enumerator(EcsQuery query)
            {
                _query = query;
                _entities = query._denseEntities;
                _count = query._entitiesCount;
                _idx = -1;
            }

            public int Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _entities[_idx];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                return ++_idx < _count;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                _query.Unlock();
            }
        }

        public sealed class Mask
        {
            EcsWorld _world;
            internal int[] Include;
            internal int[] Exclude;
            internal int IncludeCount;
            internal int ExcludeCount;
            internal int Hash;

            static readonly object SyncObj = new object();
            static Mask[] _pool = new Mask[32];
            static int _poolCount;
#if DEBUG
            bool _built;
#endif

            Mask()
            {
                Include = new int[8];
                Exclude = new int[2];
                Reset();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void Reset()
            {
                _world = null;
                IncludeCount = 0;
                ExcludeCount = 0;
                Hash = 0;
#if DEBUG
                _built = false;
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Mask Inc<T>() where T : struct
            {
                var poolId = _world.GetPool<T>().GetId();
#if DEBUG
                if (_built) { throw new Exception("Cant change built mask."); }
                if (Array.IndexOf(Include, poolId, 0, IncludeCount) != -1) { throw new Exception($"{typeof(T).Name} already in constraints list."); }
                if (Array.IndexOf(Exclude, poolId, 0, ExcludeCount) != -1) { throw new Exception($"{typeof(T).Name} already in constraints list."); }
#endif
                if (IncludeCount == Include.Length) { Array.Resize(ref Include, IncludeCount << 1); }
                Include[IncludeCount++] = poolId;
                return this;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Mask Exc<T>() where T : struct
            {
                var poolId = _world.GetPool<T>().GetId();
#if DEBUG
                if (_built) { throw new Exception("Cant change built mask."); }
                if (Array.IndexOf(Include, poolId, 0, IncludeCount) != -1) { throw new Exception($"{typeof(T).Name} already in constraints list."); }
                if (Array.IndexOf(Exclude, poolId, 0, ExcludeCount) != -1) { throw new Exception($"{typeof(T).Name} already in constraints list."); }
#endif
                if (ExcludeCount == Exclude.Length) { Array.Resize(ref Exclude, ExcludeCount << 1); }
                Exclude[ExcludeCount++] = poolId;
                return this;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public EcsQuery End(int capacity = 512)
            {
#if DEBUG
                if (_built) { throw new Exception("Cant change built mask."); }
                _built = true;
#endif
                Array.Sort(Include, 0, IncludeCount);
                Array.Sort(Exclude, 0, ExcludeCount);
                // calculate hash.
                Hash = IncludeCount + ExcludeCount;
                for (int i = 0, iMax = IncludeCount; i < iMax; i++)
                {
                    Hash = unchecked(Hash * 314159 + Include[i]);
                }
                for (int i = 0, iMax = ExcludeCount; i < iMax; i++)
                {
                    Hash = unchecked(Hash * 314159 - Exclude[i]);
                }
                var (query, isNew) = _world.GetQueryInternal(this, capacity);
                if (!isNew) { Recycle(); }
                return query;
            }

            void Recycle()
            {
                Reset();
                lock (SyncObj)
                {
                    if (_poolCount == _pool.Length)
                    {
                        Array.Resize(ref _pool, _poolCount << 1);
                    }
                    _pool[_poolCount++] = this;
                }
            }

            internal static Mask New(EcsWorld world)
            {
                lock (SyncObj)
                {
                    var mask = _poolCount > 0 ? _pool[--_poolCount] : new Mask();
                    mask._world = world;
                    return mask;
                }
            }
        }

        struct DelayedOp
        {
            public bool Added;
            public int Entity;
        }
    }
}
