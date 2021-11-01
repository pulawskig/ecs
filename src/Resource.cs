// ----------------------------------------------------------------------------
// The MIT License
// Lightweight ECS framework https://github.com/Byteron/ecs
// based on https://github.com/Leopotam/ecslite
// Copyright (c) 2021 Aaron Winter <winter.aaron93@gmail.com>
// Copyright (c) 2021 Leopotam <leopotam@gmail.com>
// ----------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;

namespace Bitron.Ecs
{
    internal struct Res<T> : IEcsAutoReset<Res<T>> where T : class
    {
        public T Value;

        public Res(T value)
        {
            Value = value;
        }

        public void AutoReset(ref Res<T> c)
        {
            c.Value = null;
        }
    }

    public static class EcsResourceExtentions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddResource<T>(this EcsWorld world, T resource) where T : class
        {
            var query = world.Query<Res<T>>().End();

            if (query.GetEntitiesCount() > 0)
            {
#if DEBUG
                throw new Exception($"AddResource<{typeof(T).Name}> resource of that type already exists.");
#else           
                var pool = query.GetPool<Res<T>>();
                
                foreach(var e in query)
                {
                    pool.Get(e) = new Res<T>(resource);
                }
#endif
            }
            else
            {
                world.Spawn().Add(new Res<T>(resource));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetResource<T>(this EcsWorld world) where T : class
        {
            var query = world.Query<Res<T>>().End();

#if DEBUG
            if (query.GetEntitiesCount() == 0)
            {
                throw new Exception($"GetResource<{typeof(T).Name}> no resource of that type exists.");
            }
#endif

            int entity = 0;
            foreach (var e in query)
            {
                entity = e;
                break;
            }

            return world.Entity(entity).Get<Res<T>>().Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetResource<T>(this EcsWorld world, out T resource) where T : class
        {
            if (world.HasResource<T>())
            {
                resource = world.GetResource<T>();
                return true;
            }

            resource = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveResource<T>(this EcsWorld world) where T : class
        {
            var query = world.Query<Res<T>>().End();

#if DEBUG
            if (query.GetEntitiesCount() == 0)
            {
                throw new Exception($"RemoveResource<{typeof(T).Name}> no resource of that type exists.");
            }
#endif

            foreach (var entity in query)
            {
                world.Entity(entity).Remove<Res<T>>();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasResource<T>(this EcsWorld world) where T : class
        {
            var query = world.Query<Res<T>>().End();
            return query.GetEntitiesCount() > 0;
        }
    }
}