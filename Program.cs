using System.Text.Json;

namespace RequestApp;

// ===== СТАТУСЫ =====
public enum Status { New, InProg, Done }

// ===== МОДЕЛЬ ЗАЯВКИ =====
public class Req
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Desc { get; set; } = "";
    public string Author { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Now;
    public Status St { get; set; } = Status.New;
    public string Priority { get; set; } = "Normal";
}

// ===== РЕПОЗИТОРИЙ =====
public interface IRepo
{
    Task<List<Req>> GetAll();
    Task<Req?> Get(Guid id);
    Task Add(Req r);
    Task Update(Req r);
    Task Delete(Guid id);
}

public class Repo : IRepo
{
    private readonly string _file = "req.json";
    private readonly JsonSerializerOptions _opt = new() { WriteIndented = true };
    
    private async Task<List<Req>> Load()
    {
        if (!File.Exists(_file)) return new List<Req>();
        var json = await File.ReadAllTextAsync(_file);
        return JsonSerializer.Deserialize<List<Req>>(json) ?? new List<Req>();
    }
    
    private async Task Save(List<Req> list) =>
        await File.WriteAllTextAsync(_file, JsonSerializer.Serialize(list, _opt));

    public async Task<List<Req>> GetAll() => await Load();
    public async Task<Req?> Get(Guid id) => (await Load()).FirstOrDefault(x => x.Id == id);
    public async Task Add(Req r) { var l = await Load(); l.Add(r); await Save(l); }
    public async Task Update(Req r) { var l = await Load(); var i = l.FindIndex(x => x.Id == r.Id); if (i >= 0) l[i] = r; await Save(l); }
    public async Task Delete(Guid id) { var l = await Load(); l.RemoveAll(x => x.Id == id); await Save(l); }
}

// ===== СЕРВИС =====
public interface IServ
{
    Task<List<Req>> GetAll();
    Task<Req?> Get(Guid id);
    Task<bool> Create(Req r);
    Task<bool> Update(Guid id, Req r);
    Task<bool> Delete(Guid id);
    Task<bool> ChangeSt(Guid id, Status st);
}

public class Serv : IServ
{
    private readonly IRepo _rep;
    public Serv(IRepo rep) => _rep = rep;
    
    public Task<List<Req>> GetAll() => _rep.GetAll();
    public Task<Req?> Get(Guid id) => _rep.Get(id);
    
    public async Task<bool> Create(Req r)
    {
        if (string.IsNullOrWhiteSpace(r.Title)) return false;
        await _rep.Add(r);
        return true;
    }
    
    public async Task<bool> Update(Guid id, Req r)
    {
        var old = await _rep.Get(id);
        if (old == null) return false;
        r.Id = id;
        await _rep.Update(r);
        return true;
    }
    
    public async Task<bool> Delete(Guid id)
    {
        var old = await _rep.Get(id);
        if (old == null) return false;
        await _rep.Delete(id);
        return true;
    }
    
    public async Task<bool> ChangeSt(Guid id, Status st)
    {
        var r = await _rep.Get(id);
        if (r == null) return false;
        r.St = st;
        await _rep.Update(r);
        return true;
    }
}

// ===== МЕНЮ (ИНТЕРФЕЙС) =====
public class Menu
{
    private readonly IServ _serv;
    public Menu(IServ serv) => _serv = serv;

    public async Task Run()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== СИСТЕМА ЗАЯВОК ===");
            Console.WriteLine("1. Список всех заявок");
            Console.WriteLine("2. Добавить заявку");
            Console.WriteLine("3. Обновить заявку");
            Console.WriteLine("4. Удалить заявку");
            Console.WriteLine("5. Сменить статус");
            Console.WriteLine("6. Выход");
            Console.Write("\nВыберите пункт: ");
            
            var choice = Console.ReadLine();
            switch (choice)
            {
                case "1": await ShowAll(); break;
                case "2": await Add(); break;
                case "3": await Update(); break;
                case "4": await Delete(); break;
                case "5": await ChangeStatus(); break;
                case "6": return;
                default: Console.WriteLine("Неверный выбор"); Console.ReadLine(); break;
            }
        }
    }

    private async Task ShowAll()
    {
        var list = await _serv.GetAll();
        Console.Clear();
        Console.WriteLine("=== ВСЕ ЗАЯВКИ ===\n");
        
        if (!list.Any())
        {
            Console.WriteLine("Заявок пока нет.");
        }
        else
        {
            foreach (var r in list)
                Console.WriteLine($"ID: {r.Id}\nТема: {r.Title}\nАвтор: {r.Author}\nСтатус: {r.St}\nПриоритет: {r.Priority}\nДата: {r.Date}\n{new string('-', 50)}");
        }
        
        Console.WriteLine("\nНажмите Enter для продолжения...");
        Console.ReadLine();
    }

    private async Task Add()
    {
        Console.Clear();
        var r = new Req();
        
        Console.Write("Тема заявки: "); r.Title = Console.ReadLine() ?? "";
        Console.Write("Описание: "); r.Desc = Console.ReadLine() ?? "";
        Console.Write("Автор: "); r.Author = Console.ReadLine() ?? "";
        Console.Write("Приоритет (Low/Normal/High): "); r.Priority = Console.ReadLine() ?? "Normal";
        
        var ok = await _serv.Create(r);
        Console.WriteLine(ok ? "\n✅ Заявка добавлена!" : "\n❌ Ошибка: тема не может быть пустой!");
        Console.ReadLine();
    }

    private async Task Update()
    {
        await ShowAll();
        Console.Write("Введите ID заявки для редактирования: ");
        if (!Guid.TryParse(Console.ReadLine(), out var id)) return;
        
        var old = await _serv.Get(id);
        if (old == null)
        {
            Console.WriteLine("Заявка не найдена!");
            Console.ReadLine();
            return;
        }
        
        Console.Write($"Новая тема (было: {old.Title}): "); var t = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(t)) old.Title = t;
        
        Console.Write($"Новое описание (было: {old.Desc}): "); var d = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(d)) old.Desc = d;
        
        await _serv.Update(id, old);
        Console.WriteLine("✅ Обновлено!");
        Console.ReadLine();
    }

    private async Task Delete()
    {
        await ShowAll();
        Console.Write("Введите ID заявки для удаления: ");
        if (Guid.TryParse(Console.ReadLine(), out var id))
        {
            await _serv.Delete(id);
            Console.WriteLine("✅ Удалено!");
        }
        Console.ReadLine();
    }

    private async Task ChangeStatus()
    {
        await ShowAll();
        Console.Write("Введите ID заявки: ");
        if (!Guid.TryParse(Console.ReadLine(), out var id)) return;
        
        Console.WriteLine("Выберите статус: 1-New, 2-InProg, 3-Done");
        var stChoice = Console.ReadLine();
        var st = stChoice switch
        {
            "1" => Status.New,
            "2" => Status.InProg,
            "3" => Status.Done,
            _ => Status.New
        };
        
        await _serv.ChangeSt(id, st);
        Console.WriteLine("✅ Статус изменен!");
        Console.ReadLine();
    }
}

// ===== ЗАПУСК =====
public class Program
{
    public static async Task Main()
    {
        var rep = new Repo();
        var serv = new Serv(rep);
        var menu = new Menu(serv);
        await menu.Run();
    }
}