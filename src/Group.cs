// ----------------------------------------------------------------------------
// The MIT License
// Lightweight ECS framework https://github.com/Byteron/ecs
// based on https://github.com/Leopotam/ecslite
// Copyright (c) 2021 Aaron Winter <winter.aaron93@gmail.com>
// Copyright (c) 2021 Leopotam <leopotam@gmail.com>
// ----------------------------------------------------------------------------

using System.Collections.Generic;

namespace Bitron.Ecs
{
    public sealed class EcsSystemGroup
    {
        List<IEcsSystem> _systems = new List<IEcsSystem>();

        public EcsSystemGroup Add(IEcsSystem system)
        {
            _systems.Add(system);
            return this;
        }

        public EcsSystemGroup OneFrame<T>() where T : struct
        {
            return Add(new RemoveAllComponentsOfType<T>());
        }

        public void Run(EcsWorld world)
        {
            for(var i = 0; i < _systems.Count; i++)
            {
                _systems[i].Run(world);
// #if DEBUG
//                 if (world.CheckForLeakedEntities()) { throw new System.Exception($"Empty entity detected in world \"{world.Name}\" after {_systems[i].GetType().Name}.Run()."); }
// #endif
            }
        }
    }
}