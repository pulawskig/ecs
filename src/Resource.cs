using System;
using System.Runtime.CompilerServices;

namespace Bitron.Ecs.Resources
{
    internal struct Res<T> where T : class {
        public T Value;

        public Res(T value)
        {
            Value = value;
        }
    }

    public static class EcsWorldExtentions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddResource<T>(this EcsWorld world, T resource) where T : class
        {
            if (world.Query<Res<T>>().End().GetEntitiesCount() > 0)
            {
                return;
            }

            world.Spawn().Add<Res<T>>();
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

            return query.Get<Res<T>>(entity).Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveResource<T>(this EcsWorld world) where T : struct
        {
            var query = world.Query<T>().End();

#if DEBUG
            if (query.GetEntitiesCount() == 0)
            {
                throw new Exception($"RemoveResource<{typeof(T).Name}> no resource of that type exists.");
            }
#endif

            foreach (var entity in query)
            {
                world.Entity(entity).Remove<T>();
            }
        }
    }
}