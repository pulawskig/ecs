using System.Collections.Generic;

namespace Bitron.Ecs
{
    public enum EcsSystemType
    {
        PreInit,
        Init,
        Run,
        Destroy,
        PostDestroy,
    }

    public sealed class EcsSystemGroup
    {
        Dictionary<EcsSystemType, List<IEcsSystem>> _allSystems;
        IEcsSystem[] _preInitSystems;
        IEcsSystem[] _initSystems;
        IEcsSystem[] _runSystems;
        IEcsSystem[] _destroySystems;
        IEcsSystem[] _postDestroySystems;

        public EcsSystemGroup Add(IEcsSystem system)
        {
            return Add(EcsSystemType.Run, system);
        }

        public EcsSystemGroup Add(EcsSystemType systemType, IEcsSystem system)
        {
            if (_allSystems.TryGetValue(systemType, out var systemList))
            {
                systemList.Add(system);
            }
            
            systemList = new List<IEcsSystem>();
            systemList.Add(system);
            _allSystems.Add(systemType, systemList);
            return this;
        }

        public void Init(EcsWorld world)
        {
            foreach(var pair in _allSystems)
            {
                var systemType = pair.Key;
                var systemList = pair.Value;

                IEcsSystem[] systemArray = null;

                switch(systemType)
                {
                    case EcsSystemType.PreInit:
                        systemArray = _preInitSystems = new IEcsSystem[systemList.Count];
                        break;
                    case EcsSystemType.Init:
                        systemArray = _initSystems = new IEcsSystem[systemList.Count];
                        break;
                    case EcsSystemType.Run:
                        systemArray = _runSystems = new IEcsSystem[systemList.Count];
                        break;
                    case EcsSystemType.Destroy:
                        systemArray = _destroySystems = new IEcsSystem[systemList.Count];
                        break;
                    case EcsSystemType.PostDestroy:
                        systemArray = _postDestroySystems = new IEcsSystem[systemList.Count];
                        break;
                }

                for(int i = 0; i < systemList.Count; i++)
                {
                    systemArray[i] = systemList[i];
                }
            }

            foreach (var system in _preInitSystems)
            {
                system.Run(world);
#if DEBUG
                if (world.CheckForLeakedEntities()) { throw new System.Exception($"Empty entity detected in world \"{world.Name}\" after {system.GetType().Name}.Run()."); }
#endif
            }

            foreach (var system in _initSystems)
            {
                system.Run(world);
#if DEBUG
                if (world.CheckForLeakedEntities()) { throw new System.Exception($"Empty entity detected in world \"{world.Name}\" after {system.GetType().Name}.Run()."); }
#endif
            }
        }

        public void Run(EcsWorld world)
        {
            foreach(var system in _runSystems)
            {
                system.Run(world);
#if DEBUG
                if (world.CheckForLeakedEntities()) { throw new System.Exception($"Empty entity detected in world \"{world.Name}\" after {system.GetType().Name}.Run()."); }
#endif
            }
        }

        public void Destroy(EcsWorld world)
        {
            foreach(var system in _destroySystems)
            {
                system.Run(world);
#if DEBUG
                if (world.CheckForLeakedEntities()) { throw new System.Exception($"Empty entity detected in world \"{world.Name}\" after {system.GetType().Name}.Run()."); }
#endif
            }

            foreach(var system in _postDestroySystems)
            {
                system.Run(world);
#if DEBUG
                if (world.CheckForLeakedEntities()) { throw new System.Exception($"Empty entity detected in world \"{world.Name}\" after {system.GetType().Name}.Run()."); }
#endif
            }

            _allSystems.Clear();
            _preInitSystems = null;
            _initSystems = null;
            _runSystems = null;
            _destroySystems = null;
            _postDestroySystems = null;
        }
    }
}