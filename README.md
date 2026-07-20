# FTO - Sistema de Gestão e Controle de Vendas

Sistema desktop desenvolvido para facilitar o controle financeiro, gestão de clientes e vendas da empresa FTO. O software oferece uma interface intuitiva, suporte a temas (claro/escuro) e **atualizações automáticas via GitHub Releases**.

Documentação de publicação e update: **[guia.md](guia.md)**

---

## Estrutura do projeto

```
FTO-Main/
├── guia.md                          # Guia de releases e atualização automática
├── README.md
├── .github/workflows/release.yml    # Build + ZIP na Release
└── FTO_Sistema/
    └── FTO_App/
        ├── Services/
        │   └── UpdateService.cs     # Verifica/baixa update do GitHub
        ├── views/
        │   └── LoginView.*          # Botão "Atualizar sistema"
        ├── models/
        ├── Database.cs
        └── FTO_App.csproj
```

---

## Funcionalidades

- **Controle de Acesso:** Login seguro com usuário e senha.
- **Gestão de Vendas:** Lançamento com lucro, filtros por data/cliente/status.
- **Gestão de Clientes:** Cadastro, histórico, CPF/CNPJ.
- **Estoque e Analytics:** Módulos de produtos e painel financeiro.
- **Relatórios:** Excel (.xlsx) e PDF (clientes e cupom).
- **Impressão térmica:** Cupom não fiscal (ex. MP-2500 HT).
- **Atualização automática:** Botão na tela de login puxa o ZIP da última Release.

---

## Configuração da empresa (`.env`)

Dados da empresa ficam em `FTO_Sistema/FTO_App/.env` (copiado no publish).

Use `.env.example` como modelo. **Nunca** commit o `.env`.

Opcional para repositório **privado**:

```env
FTO_UPDATE_TOKEN=ghp_seu_token
```

---

## Gerar executável (local)

Na raiz do repositório:

```powershell
dotnet publish "FTO_Sistema\FTO_App\FTO_App.csproj" -p:PublishProfile=Win64-SelfContained
```

Saída: `FTO_Sistema\publish\FTO_App-win-x64\FTO_App.exe`

**Entrega manual:** compacte a pasta inteira em `.zip`. O cliente deve extrair e rodar `FTO_App.exe` (não só o `.exe`).

---

## Atualização automática

1. Na tela de login, clique em **Atualizar sistema**.
2. Se houver Release mais nova no GitHub, o app baixa `FTO_App-win-x64.zip`, aplica e reinicia.
3. **Preservados:** `.env`, `FTO.db` (e arquivos WAL).

### Publicar update (resumo)

1. `git push` das alterações  
2. Criar Release com tag `vX.Y.Z` (maior que a versão atual)  
3. Aguardar Action **Build Release Package** anexar o ZIP  
4. Cliente usa o botão na login  

Passo a passo completo: **[guia.md](guia.md)**

---

## Tecnologias

| Item | Tecnologia |
|------|------------|
| UI | C# / WPF (.NET 8) |
| Banco | SQLite (WAL) |
| Excel | ClosedXML |
| PDF | QuestPDF |
| Update | GitHub Releases API |

---

## Requisitos

- Windows 10/11 64-bits  
- Build self-contained: cliente **não** precisa instalar .NET  
- Conexão com internet para verificar/atualizar  

---

## Como instalar (primeira vez)

1. Baixe `FTO_App-win-x64.zip` da [Release mais recente](https://github.com/kapimkk/FTO-Main/releases/latest)  
2. Extraia (ex. `C:\FTO Sistema`)  
3. Configure o `.env`  
4. Execute `FTO_App.exe`  
