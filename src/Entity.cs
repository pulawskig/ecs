// ----------------------------------------------------------------------------
// The MIT License
// Lightweight ECS framework https://github.com/Byteron/ecs
// based on https://github.com/Leopotam/ecslite
// Copyright (c) 2021 Aaron Winter <winter.aaron93@gmail.com>
// Copyright (c) 2021 Leopotam <leopotam@gmail.com>
// ----------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace Bitron.Ecs
{
    public struct EcsEntity
    {
        internal int Id;
        internal int Gen;
        internal EcsWorld World;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EcsEntity Add<T>() where T : struct
        {
            var pool = World.GetPool<T>();
            pool.Add(Id);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EcsEntity Add<T>(T component) where T : struct
        {
            var pool = World.GetPool<T>();
            pool.Add(Id) = component;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Get<T>() where T : struct
        {
            var pool = World.GetPool<T>();
            return ref pool.Get(Id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EcsEntity Remove<T>() where T : struct
        {
            var pool = World.GetPool<T>();
            pool.Remove(Id);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetId()
        {
            return Id;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAlive()
        {
            if (World == null 
                || !World.IsAlive() 
                || !World.IsEntityAliveInternal(Id) 
                || World.GetEntityGen(Id) != Gen)
            {
                return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Destroy()
        {
            World.DespawnEntity(Id);
        }
#if DEBUG
        // For using in IDE debugger.
        internal object[] DebugComponentsView
        {
            get
            {
                object[] list = null;
                if (IsAlive())
                {
                    World.GetComponents(Id, ref list);
                }
                return list;
            }
        }
        // For using in IDE debugger.
        internal int DebugComponentsCount
        {
            get
            {
                if (IsAlive())
                {
                    return World.GetComponentsCount(Id);
                }
                return 0;
            }
        }

        // For using in IDE debugger.
        public override string ToString()
        {
            if (Id == 0 && Gen == 0) { return "Entity-Null"; }
            if (World == null || !World.IsAlive() || !World.IsEntityAliveInternal(Id) || World.GetEntityGen(Id) != Gen) { return "Entity-NonAlive"; }
            System.Type[] types = null;
            var count = World.GetComponentTypes(Id, ref types);
            System.Text.StringBuilder sb = null;
            if (count > 0)
            {
                sb = new System.Text.StringBuilder(512);
                for (var i = 0; i < count; i++)
                {
                    if (sb.Length > 0) { sb.Append(","); }
                    sb.Append(types[i].Name);
                }
            }
            return $"Entity-{Id}:{Gen} [{sb}]";
        }
#endif
    }

    public static class EcsEntityExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EcsEntity Spawn(this EcsWorld world)
        {
            var entity = new EcsEntity();
            entity.Id = world.SpawnEntity();
            entity.Gen = world.GetEntityGen(entity.Id);
            entity.World = world;
            return entity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EcsEntity Entity(this EcsWorld world, int entityId)
        {
            EcsEntity entity;
            entity.World = world;
            entity.Id = entityId;
            entity.Gen = world.GetEntityGen(entityId);
            return entity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EqualsTo(this in EcsEntity a, in EcsEntity b)
        {
            return a.Id == b.Id && a.Gen == b.Gen && a.World == b.World;
        }
    }
}
