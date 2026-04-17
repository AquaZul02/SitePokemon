using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<DataStorage>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", async (HttpContext http, DataStorage storage) =>
{
    if (http.User?.Identity?.IsAuthenticated == true)
    {
        http.Response.Redirect("/team");
        return;
    }

    await http.Response.WriteAsync(HtmlPage("Bem-vindo ao Pokémon Team Manager", @"
        <form method='post' action='/login'>
            <h2>Entrar</h2>
            <label>Usuário:<br><input name='username' required></label><br>
            <label>Senha:<br><input type='password' name='password' required></label><br>
            <button type='submit'>Entrar</button>
        </form>
        <hr>
        <form method='post' action='/register'>
            <h2>Cadastrar</h2>
            <label>Usuário novo:<br><input name='username' required></label><br>
            <label>Senha nova:<br><input type='password' name='password' required></label><br>
            <button type='submit'>Cadastrar</button>
        </form>
    "));
});

app.MapPost("/register", async (HttpContext http, DataStorage storage) =>
{
    var form = await http.Request.ReadFormAsync();
    var username = form["username"].ToString().Trim();
    var password = form["password"].ToString();

    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
    {
        await http.Response.WriteAsync(HtmlPage("Erro", "<p>Usuário e senha são obrigatórios.</p><p><a href='/'>Voltar</a></p>"));
        return;
    }

    if (storage.UserExists(username))
    {
        await http.Response.WriteAsync(HtmlPage("Erro", "<p>Usuário já existe.</p><p><a href='/'>Voltar</a></p>"));
        return;
    }

    storage.AddUser(username, password);
    await http.Response.WriteAsync(HtmlPage("Cadastro concluído", "<p>Cadastro realizado com sucesso.</p><p><a href='/'>Ir para o login</a></p>"));
});

app.MapPost("/login", async (HttpContext http, DataStorage storage) =>
{
    var form = await http.Request.ReadFormAsync();
    var username = form["username"].ToString().Trim();
    var password = form["password"].ToString();

    if (!storage.ValidateUser(username, password))
    {
        await http.Response.WriteAsync(HtmlPage("Login inválido", "<p>Usuário ou senha inválidos.</p><p><a href='/'>Voltar</a></p>"));
        return;
    }

    var claims = new List<Claim> { new Claim(ClaimTypes.Name, username) };
    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
    http.Response.Redirect("/team");
});

app.MapGet("/team", [Authorize] async (HttpContext http, DataStorage storage, HttpClient httpClient) =>
{
    var username = http.User.Identity?.Name!;
    var team = storage.GetTeam(username);
    var pokemonInputs = new StringBuilder();
    var teamImages = new StringBuilder();

    if (team?.Pokemons.Any() == true)
    {
        teamImages.Append("<div style='display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:16px;margin-bottom:24px;'>");
        foreach (var pokemon in team.Pokemons)
        {
            var spriteUrl = await GetPokemonSpriteUrl(httpClient, pokemon);
            teamImages.Append($"<div style='border:1px solid #ddd;padding:12px;border-radius:8px;text-align:center;'>");
            teamImages.Append($"<strong>{System.Net.WebUtility.HtmlEncode(pokemon)}</strong><br>");
            teamImages.Append(spriteUrl is not null
                ? $"<img src='{spriteUrl}' alt='{System.Net.WebUtility.HtmlEncode(pokemon)}' style='max-width:120px;margin-top:12px;'>"
                : "<span style='color:#a00;'>Imagem não encontrada</span>");
            teamImages.Append("</div>");
        }
        teamImages.Append("</div>");
    }

    for (var i = 0; i < 6; i++)
    {
        var value = team?.Pokemons.ElementAtOrDefault(i) ?? string.Empty;
        pokemonInputs.Append($"<label>Pokémon {i + 1}:<br><input name='pokemon{i}' value='{System.Net.WebUtility.HtmlEncode(value)}'></label><br>");
    }

    await http.Response.WriteAsync(HtmlPage($"Time de {username}", $@"
        <p>Bem-vindo, <strong>{System.Net.WebUtility.HtmlEncode(username)}</strong>!</p>
        {teamImages}
        <form method='post' action='/team'>
            {pokemonInputs}
            <button type='submit'>Salvar time</button>
        </form>
        <p><a href='/logout'>Sair</a></p>
    "));
});

app.MapPost("/team", [Authorize] async (HttpContext http, DataStorage storage, HttpClient httpClient) =>
{
    var username = http.User.Identity?.Name!;
    var form = await http.Request.ReadFormAsync();
    var pokemons = new List<string>();

    for (var i = 0; i < 6; i++)
    {
        var value = form[$"pokemon{i}"] .ToString().Trim();
        if (string.IsNullOrEmpty(value))
        {
            continue;
        }

        if (await GetPokemonSpriteUrl(httpClient, value) is null)
        {
            await http.Response.WriteAsync(HtmlPage("Pokémon inválido", $"<p>O Pokémon '<strong>{System.Net.WebUtility.HtmlEncode(value)}</strong>' não foi encontrado na API.</p><p><a href='/team'>Voltar ao time</a></p>"));
            return;
        }

        pokemons.Add(value);
    }

    storage.SaveTeam(username, pokemons);
    http.Response.Redirect("/team");
});

app.MapGet("/logout", [Authorize] async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    http.Response.Redirect("/");
});

app.Run();

static string HtmlPage(string title, string body)
{
    return $"<!DOCTYPE html><html lang='pt-BR'><head><meta charset='utf-8'><title>{title}</title><style>body{{font-family:Segoe UI,Arial,sans-serif;margin:40px;max-width:720px;}}input{{width:100%;padding:8px;margin:6px 0;}}button{{padding:10px 16px;}}label{{display:block;margin-top:10px;}}img{{max-width:100%;height:auto;border-radius:12px;}}</style></head><body><h1>{title}</h1>{body}</body></html>";
}

static async Task<string?> GetPokemonSpriteUrl(HttpClient httpClient, string pokemonName)
{
    if (string.IsNullOrWhiteSpace(pokemonName))
    {
        return null;
    }

    var normalized = pokemonName.Trim().ToLowerInvariant();
    try
    {
        var response = await httpClient.GetFromJsonAsync<PokeApiPokemon>($"https://pokeapi.co/api/v2/pokemon/{System.Net.WebUtility.UrlEncode(normalized)}");
        return response?.Sprites?.FrontDefault;
    }
    catch
    {
        return null;
    }
}

internal sealed class PokeApiPokemon
{
    [JsonPropertyName("sprites")]
    public PokeApiSprites? Sprites { get; set; }
}

internal sealed class PokeApiSprites
{
    [JsonPropertyName("front_default")]
    public string? FrontDefault { get; set; }
}

internal class DataStorage
{
    private readonly string _storagePath;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public DataStorage(IWebHostEnvironment env)
    {
        _storagePath = env.ContentRootPath;
        EnsureStorageFiles();
    }

    public bool UserExists(string username)
    {
        var data = LoadData();
        return data.Users.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
    }

    public void AddUser(string username, string password)
    {
        var data = LoadData();
        var user = User.Create(username, password);
        data.Users.Add(user);
        SaveData(data);
    }

    public bool ValidateUser(string username, string password)
    {
        var data = LoadData();
        var user = data.Users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        return user?.VerifyPassword(password) == true;
    }

    public PokemonTeam? GetTeam(string username)
    {
        var data = LoadData();
        return data.Teams.FirstOrDefault(t => string.Equals(t.Username, username, StringComparison.OrdinalIgnoreCase));
    }

    public void SaveTeam(string username, List<string> pokemons)
    {
        var data = LoadData();
        var team = data.Teams.FirstOrDefault(t => string.Equals(t.Username, username, StringComparison.OrdinalIgnoreCase));
        if (team is null)
        {
            data.Teams.Add(new PokemonTeam { Username = username, Pokemons = pokemons });
        }
        else
        {
            team.Pokemons = pokemons;
        }

        SaveData(data);
    }

    private void EnsureStorageFiles()
    {
        if (!Directory.Exists(_storagePath))
            Directory.CreateDirectory(_storagePath);

        var file = Path.Combine(_storagePath, "storage.json");
        if (!File.Exists(file))
        {
            var initial = new StorageData { Users = new List<User>(), Teams = new List<PokemonTeam>() };
            File.WriteAllText(file, System.Text.Json.JsonSerializer.Serialize(initial, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private StorageData LoadData()
    {
        _mutex.Wait();
        try
        {
            var file = Path.Combine(_storagePath, "storage.json");
            var json = File.ReadAllText(file);
            return System.Text.Json.JsonSerializer.Deserialize<StorageData>(json) ?? new StorageData();
        }
        finally
        {
            _mutex.Release();
        }
    }

    private void SaveData(StorageData data)
    {
        _mutex.Wait();
        try
        {
            var file = Path.Combine(_storagePath, "storage.json");
            File.WriteAllText(file, System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            _mutex.Release();
        }
    }

    private sealed class StorageData
    {
        public List<User> Users { get; set; } = new();
        public List<PokemonTeam> Teams { get; set; } = new();
    }
}

internal sealed class User
{
    public string Username { get; set; } = string.Empty;
    public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
    public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();

    public static User Create(string username, string password)
    {
        using var rng = RandomNumberGenerator.Create();
        var salt = new byte[16];
        rng.GetBytes(salt);
        var hash = HashPassword(password, salt);
        return new User { Username = username, PasswordHash = hash, PasswordSalt = salt };
    }

    public bool VerifyPassword(string password)
    {
        var hash = HashPassword(password, PasswordSalt);
        return hash.SequenceEqual(PasswordHash);
    }

    private static byte[] HashPassword(string password, byte[] salt)
    {
        using var deriveBytes = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        return deriveBytes.GetBytes(32);
    }
}

internal sealed class PokemonTeam
{
    public string Username { get; set; } = string.Empty;
    public List<string> Pokemons { get; set; } = new();
}
