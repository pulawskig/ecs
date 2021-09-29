namespace Bitron.Ecs
{
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