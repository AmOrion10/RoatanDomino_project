namespace Domain;

public class Chat(int id, string name)
{
    public int Id { get; } = id;
    public List<User> Users { get; set; } = [];
    public string Name { get; } = name;
}