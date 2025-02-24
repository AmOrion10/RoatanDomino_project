namespace Application.Interfaces.Repositories;

public interface INationalityRepository
{
    Task<long?> GetByName(string name);
}
