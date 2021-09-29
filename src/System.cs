// ----------------------------------------------------------------------------
// The MIT License
// Lightweight ECS framework https://github.com/Byteron/ecs
// based on https://github.com/Leopotam/ecslite
// Copyright (c) 2021 Aaron Winter <winter.aaron93@gmail.com>
// Copyright (c) 2021 Leopotam <leopotam@gmail.com>
// ----------------------------------------------------------------------------

namespace Bitron.Ecs
{
    public interface IEcsSystem
    {
        void Run(EcsWorld world);
    }

    public class RemoveAllComponentsOfType<T> : IEcsSystem where T : struct
    {
        public void Run(EcsWorld world)
        {
            var query = world.Query<T>().End();
            var pool = query.GetPool<T>();

            foreach (var entity in query)
            {
                pool.Remove(entity);
            }
        }
    }
}
