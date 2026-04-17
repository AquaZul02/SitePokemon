# SitePokemon

Projeto Web ASP.NET Core minimal API para gerenciar um time de Pokémon.

## Objetivo de aprendizado

Este projeto foi criado para estudar:

- ASP.NET Core minimal APIs
- Autenticação com cookies
- Persistência simples em arquivo JSON
- Consumo de APIs externas com `HttpClient`
- Integração com a PokéAPI (`https://pokeapi.co/`)
- Renderização de HTML diretamente em rotas

## Visão geral do funcionamento

O aplicativo expõe rotas básicas para registro, login, visualização e edição de um time de Pokémon.

### Fluxo principal

1. O usuário acessa `/`
2. Se não estiver autenticado, vê um formulário de login e cadastro
3. Após login, é redirecionado para `/team`
4. Em `/team`, o usuário informa até 6 nomes de Pokémon
5. Cada nome é validado usando a PokéAPI
6. Se válido, o time é salvo localmente em `storage.json`
7. A página mostra as imagens dos Pokémon salvos

## Arquitetura do projeto

O código está todo em `Program.cs` usando o estilo de minimal API do ASP.NET Core.

### Serviços registrados

- `AddAuthentication` e `AddAuthorization`: para habilitar autenticação por cookie
- `AddHttpClient`: para chamar a PokéAPI
- `AddSingleton<DataStorage>`: para acesso ao repositório de dados em JSON

### Middleware configurado

- `app.UseStaticFiles()`
- `app.UseRouting()`
- `app.UseAuthentication()`
- `app.UseAuthorization()`

## Rotas disponíveis

### `GET /`

Página inicial que apresenta:

- formulário de login
- formulário de cadastro

### `POST /register`

Cria um usuário novo.

Validações:

- nome e senha obrigatórios
- usuário não pode existir

### `POST /login`

Autentica o usuário.

### `GET /team`

Mostra o time salvo do usuário autenticado.

- Exibe até 6 campos para nome de Pokémon
- Mostra as imagens dos Pokémon existentes
- Usa `GetPokemonSpriteUrl` para consultar a PokéAPI

### `POST /team`

Valida e salva o time:

- ignora campos vazios
- usa a PokéAPI para verificar se o Pokémon existe
- impede o salvamento se algum nome for inválido

### `GET /logout`

Desloga o usuário e redireciona para `/`

## Estruturas de dados internas

### `DataStorage`

Classe responsável por:

- criar `storage.json` se não existir
- carregar e salvar dados
- gerenciar usuários e times

Métodos importantes:

- `UserExists(username)`
- `AddUser(username, password)`
- `ValidateUser(username, password)`
- `GetTeam(username)`
- `SaveTeam(username, pokemons)`

### `User`

Representa um usuário registrado.

- `Username`
- `PasswordHash`
- `PasswordSalt`

A senha é protegida com PBKDF2 (`Rfc2898DeriveBytes`) e SHA-256.

### `PokemonTeam`

Modelo simples que guarda:

- `Username`
- `List<string> Pokemons`

## Integração com a PokéAPI

A função `GetPokemonSpriteUrl(HttpClient, string)` faz:

1. normaliza o nome do Pokémon para minúsculas
2. chama `https://pokeapi.co/api/v2/pokemon/{nome}`
3. desserializa o JSON retornado
4. retorna a URL da imagem `sprites.front_default`

Se a API não encontrar o Pokémon, retorna `null`.

### Classe de mapeamento

- `PokeApiPokemon`
- `PokeApiSprites`

Com atributos `JsonPropertyName` para mapear os campos JSON corretamente.

## Como executar

No diretório do projeto:

```bash
cd /home/joao/SitePokemon
dotnet run
```

Acesse o site em:

- `http://localhost:5000`
- ou `https://localhost:5001`

## Como testar manualmente

1. Abra o app no navegador
2. Cadastre um usuário
3. Faça login
4. Vá em `/team`
5. Insira nomes de Pokémon válidos, por exemplo:
   - `pikachu`
   - `charizard`
   - `bulbasaur`
6. Salve e verifique se as imagens aparecem

## Pontos de aprendizado

- como usar cookies para autenticar uma aplicação web
- como renderizar HTML a partir de rotas simples
- como armazenar estado de forma persistente com JSON
- como consumir APIs REST externas e tratar erros
- como mapear JSON para classes C# usando `System.Text.Json`

## Possíveis melhorias

- separar o código em camadas
- adicionar testes automatizados (`xUnit`)
- usar `Razor Pages` ou `Blazor` para UI melhor
- tratar a PokéAPI de forma mais completa (tipos, altura, peso)
- adicionar página de perfil e lista de times
- melhorar segurança e criptografia de senha

## Observações

O arquivo `storage.json` é gerado automaticamente e armazena:

- usuários cadastrados
- times de Pokémon

Nunca use este mecanismo em produção sem proteção adicional.
