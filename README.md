# LeoEcsLite - Lightweight C# Entity Component System framework
Performance, zero/small memory allocations/footprint, no dependencies on any game engine - main goals of this project.

> **Important!** Don't forget to use `DEBUG` builds for development and `RELEASE` builds in production: all internal error checks / exception throwing works only in `DEBUG` builds and eleminated for performance reasons in `RELEASE`.

> **Important!** LeoEcsLite API **is not thread safe** and will never be! If you need multithread-processing - you should implement it on your side as part of ecs-system.

# Table of content
* [Installation](#installation)
    * [As source](#as-source)
* [Main parts of ecs](#main-parts-of-ecs)
    * [Entity](#entity)
    * [Component](#component)
    * [System](#system)
* [Special classes](#special-classes)
    * [EcsPool](#ecspool)
    * [EcsQuery](#ecsfilter)
    * [EcsWorld](#ecsworld)
    * [Custom engine](#custom-engine)
    * [With sources](#with-sources)
* [License](#license)


# Installation

## As source
If you can't / don't want to use unity modules, code can be cloned or downloaded as archive from `releases` page.

# Main parts of ecs

## Entity
Сontainer for components. Implemented as `int`:
```csharp
// Creates new entity in world context.
int entity = _world.NewEntity ();

// Any entity can be destroyed. All components will be removed first, then entity will be destroyed. 
world.DelEntity (entity);
```

> **Important!** Entities can't live without components and will be killed automatically after last component removement.

## Component
Container for user data without / with small logic inside:
```csharp
struct Component1 {
    public int Id;
    public string Name;
}
```
Components can be added / requested / removed through [component pools](#ecspool).

## System
Сontainer for logic for processing queryed entities. User class should implement `IEcsInitSystem`, `IEcsDestroySystem`, `IEcsRunSystem` (or other supported) interfaces:
```csharp
class UserSystem : IEcsStstem {
    
    public void Run (EcsSystems systems) {
        // Will be called on each EcsSystems.Run() call.
    }
}
```

# Special classes

## EcsPool
Container for components, provides api for adding / requesting / removing components on entity:
```csharp
int entity = world.NewEntity ();
EcsPool<Component1> pool = world.GetPool<Component1> (); 

// Add() adds component to entity. If component already exists - exception will be raised in DEBUG.
ref Component1 c1 = ref pool.Add (entity);

// Get() returns exist component on entity. If component does not exists - exception will be raised in DEBUG.
ref Component1 c1 = ref pool.Get (entity);

// Del() removes component from entity. If it was last component - entity will be removed automatically too.
pool.Del (entity);
```

> **Important!** After removing component will be pooled and can be reused later. All fields will be reset to default values automatically.

## EcsQuery
Container for keeping queryed entities with specified component list:
```csharp
class WeaponSystem : IEcsInitSystem, IEcsRunSystem {
    public void Init (EcsSystems systems) {
        // We want to get default world instance...
        EcsWorld world = systems.GetWorld ();
        
        // and create test entity...
        int entity = world.NewEntity ();
        
        // with "Weapon" component on it.
        var weapons = world.GetPool<Weapon>();
        weapons.Add (entity);
    }

    public void Run (EcsSystems systems) {
        EcsWorld world = systems.GetWorld ();
        // We want to get entities with "Weapon" and without "Health".
        // You can cache this query somehow if you want.
        var query = world.Query<Weapon> ().Exc<Health> ().End ();
        
        // We want to get pool of "Weapon" components.
        // You can cache this pool somehow if you want.
        var weapons = world.GetPool<Weapon>();
        
        foreach (int entity in query) {
            ref Weapon weapon = ref weapons.Get (entity);
            weapon.Ammo = System.Math.Max (0, weapon.Ammo - 1);
        }
    }
}
```

Additional constraints can be added with `Inc<>()` / `Exc<>()` methods.

> Important: Any query supports any amount of components, include and exclude lists can't intersect and should be unique.

## EcsWorld
Root level container for all entities / components, works like isolated environment.

> Important: Do not forget to call `EcsWorld.Destroy()` method if instance will not be used anymore.




# License
The software is released under the terms of the [MIT license](./LICENSE.md).

No personal support or any guarantees.
