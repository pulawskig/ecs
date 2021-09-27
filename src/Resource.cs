// ----------------------------------------------------------------------------
// The MIT License
// Lightweight ECS framework https://github.com/Byteron/ecs
// based on https://github.com/Leopotam/ecslite
// Copyright (c) 2021 Aaron Winter <winter.aaron93@gmail.com>
// Copyright (c) 2021 Leopotam <leopotam@gmail.com>
// ----------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;

namespace Bitron.Ecs.Resources
{
    internal struct Res<T> where T : struct
    {
        public T Value;

        public Res(T value)
        {
            Value = value;
        }
    }

    public static class EcsWorldExtentions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddResource<T>(this EcsWorld world, T resource) where T : struct
        {
            if (world.Query<Res<T>>().End().GetEntitiesCount() > 0)
            {
#if DEBUG
                throw new Exception($"AddResource<{typeof(T).Name}> resource of that type already exists.");
#else
                world.GetResource<T>() = resource;
#endif
            }
            else
            {
                world.Spawn().Add<Res<T>>();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T GetResource<T>(this EcsWorld world) where T : struct
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

            return ref query.Get<Res<T>>(entity).Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveResource<T>(this EcsWorld world) where T : struct
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
    }
}