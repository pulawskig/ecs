# BitEcs - Lightweight C# Entity Component System framework
Ergonomic, performant, zero/small memory allocations/footprint, no dependencies on any game engine - main goals of this project.

> **Important!** Don't forget to use `DEBUG` builds for development and `RELEASE` builds in production: all internal error checks / exception throwing works only in `DEBUG` builds and eleminated for performance reasons in `RELEASE`.

> **Important!** BitEcs API **is not thread safe** and will never be! If you need multithread-processing - you should implement it on your side as part of ecs-system.
# Basics

## World
Container for all your data.
```csharp
// Creates a new world.
EcsWorld world = new EcsWorld();

// Creates a new named world.
EcsWorld world = new EcsWorld("Name");

// Destroys a world;
world.Destroy();
```

## Entity
Сontainer for components. Implemented as `int`:
```csharp
// Creates new entity in world context.
EcsEntity entity = _world.Spawn();

// Any entity can be destroyed. 
// All components will be removed first, then entity will be destroyed. 
entity.Destoy();
```

> **Important!** Entities can't live without components and will be killed automatically after last component removement.

## Component
Container for user data without / with small logic inside:
```csharp
struct Component1
{
    public int Id;
    public string Name;
}
```
Components can be added and removed directly through entities.
```csharp
// creates entity
EcsEntity entity = world.Spawn();

// adds component to entity.
entity.Add<Component1>();

// uou can also add multiple components at once
entity.Add<Component2>().Add<Component3>();

// or add a predefined struct as component
entity.Add(new Component4("Value"));

// removing components is just as easy
entity.Remove<Component1>().Remove<Component4>();
```
if you have an EcsEntity in your hand, you can also get components from it.
```csharp
// get a component from an entity
ref var c1 = ref entity.Get<Component1>();
ref var c2 = ref entity.Get<Component2>();
```
> **Important!** If you try to get a Component the Entity does not have your game will crash!
## System
Сontainer for logic for processing entities.
```csharp
class CustomSystem : IEcsSystem
{
    public void Run(EcsWorld world)
    {
        // Will be called on each EcsSystems.Run() call.
    }
}
```
Run systems like so:
```csharp
// Systems are run on Worlds.
EcsWorld world = new EcsWorld();

// create an instance of the system;
IEcsSystem system = new CustomSystem();

// run the system on a world
system.Run(world);
```
## Query
to iterate components in you system, you need to create queries.
```csharp
class CustomSystem : IEcsSystem
{
    public void Run(EcsWorld world)
    {
        // create a query
        var query = world.Query<Component1>()
        
        // iterate the query
        // query iteration gives you entity ids.
        foreach (int entityId in query)
        {
            // get components from queries
            ref var c1 = ref query.Get<Component1>();

            // you cannot get components not included in the query
            // even if the entity has a Component2
            ref var c2 = ref query.Get<Component2>(); // WILL CRASH!

            // if you do want access to other components,
            // you can do so through world
            ref var c2 = ref world.Entity(entityId).Get<Component2>();
        }
    }
}
```
You can put multiple constraints on your queries
```csharp
// create a query with multiple constraints
var query = world.Query<Component1>().Inc<Component2>().Exc<Component3>();
```
> Important: Any query supports any amount of components, include and exclude lists can't intersect and should be unique.
# Resource
Instance of any custom type can be shared between all systems:
```csharp
struct CustomResource
{
    public string Path;
}
```
```csharp
// create resource
CustomResource res = new CustomResource { Path = "Items/{0}" };

// add resource to world
world.AddResource(res);

// or add resource with default values
world.AddResource<CustomResource>();
```
```csharp
class System : IEcsSystem
{
    public void Run(EcsWorld world)
    {
        // get resource from world
        ref CustomResource res = ref systems.GetResource<CustomResource>(); 
        
        string path = string.Format(res.Path, 123);
        // path == "Items/123" here.
    } 
}
```
> Important: Resources are unique. If you try to add another instance of a same resource type, the present one will be overwritten by the new one.
# Special classes

## EcsPool
Container for components.
EcsEntity and Queries wrap around these pools for a more ergonomic API, but at a performance cost.
If you need more performance, you can optimize your code using pools directly.
```csharp
int entityId = world.Spawn().GetId();
EcsPool<Component1> pool = world.GetPool<Component1>(); 

// Add() adds component to entity. If component already exists - exception will be raised in DEBUG.
ref Component1 c1 = ref pool.Add(entityId);

// Get() returns exist component on entity. If component does not exists - exception will be raised in DEBUG.
ref Component1 c1 = ref pool.Get(entityId);

// Del() removes component from entity. If it was last component - entity will be removed automatically too.
pool.Remove(entityId);
```
```csharp
class CustomSystem : IEcsSystem
{
    public void Run(EcsWorld world)
    {
        // create a query
        var query = world.Query<Component1>();
        var pool = query.GetPool<Component1>();
        
        // iterate the query
        // query iteration gives you entity ids.
        foreach (int entityId in query)
        {
            // get components from pool directly
            ref var c1 = ref pool.Get<Component1>(entityId);
        }
    }
}
```
> **Important!** After removing component will be pooled and can be reused later. All fields will be reset to default values automatically.
## SystemGroup
You can group Systems together into an EcsSystemGroup
```csharp
// create a new system group
EcsSystemGroup systemGroup = new EcsSystemGroup();

systemGroup
    .Add(EcsSystemType.PreInit, new PreInitSystem())
    .Add(EcsSystemType.Init, new InitSystem())
    .Add(EcsSystemType.Run, new RunSystem())
    .Add(EcsSystemType.Destroy, new DestroySystem())
    .Add(EcsSystemType.PostDestroy, new PostDestroySystem());

// systems are added as RunSystem per default;
systemGroup.Add(new AnotherRunSystem());

// like systems, system groups are run on a world
EcsWorld world = new EcsWorld();

// run init systems
systemGroup.Init(world);

// run run systems
systemGroup.Run(world);

// run destroy systems
systemGroup.Destroy(world);
```
> Important: Do not forget to call `EcsSystemGroup.Destroy()` method if instance will not be used anymore.

## Custom engine
> C#7.3 or above required for this framework.

Code example - each part should be integrated in proper place of engine execution flow.
```csharp
using Bitron.Ecs;

class Engine
{
    EcsWorld _world;
    EcsSystemGroup _systems;

    // Initialization of ecs world and systems.
    void Init()
    {        
        _world = new EcsWorld();
        _systemGroup = new EcsSystemGroup();
        _systemGroup
            // register your systems here, for example:
            // .Add(new TestSystem1())
            // .Add(new TestSystem2())
            .Init(_world);
    }

    // Engine update loop.
    void UpdateLoop()
    {
        _systemGroup?.Run(_world);
    }

    // Cleanup.
    void Destroy()
    {
        if (_systemGroup != null)
        {
            _systemGroup.Destroy(_world);
            _systemGroup = null;
        }
        if (_world != null)
        {
            _world.Destroy();
            _world = null;
        }
    }
}
```
# License
The software is released under the terms of the [MIT license](./LICENSE.md).

No personal support or any guarantees.

# FAQ

### I copy&paste my reset components code again and again. How can I do it in other manner?

If you want to simplify your code and keep reset/init code at one place, you can setup custom handler to process cleanup / initialization for component:
```csharp
struct MyComponent : IEcsAutoReset<MyComponent>
{
    public int Id;
    public object LinkToAnotherComponent;

    public void AutoReset(ref MyComponent c)
    {
        c.Id = 2;
        c.LinkToAnotherComponent = null;
    }
}
```
This method will be automatically called for brand new component instance and after component removing from entity and before recycling to component pool.
> Important: With custom `AutoReset` behaviour there are no any additional checks for reference-type fields, you should provide correct cleanup/init behaviour without possible memory leaks.