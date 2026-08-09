# FTO - Sistema de Gestão e Controle de Vendas

Sistema desktop para controle financeiro, clientes, estoque, vendas e preparação de NF-e da empresa FTO. Interface com temas (claro/escuro), navegação unificada pós-login e **atualizações automáticas via GitHub Releases**.

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
        │   ├── UpdateService.cs           # Verifica/baixa update do GitHub
        │   ├── EmpresaConfigStore.cs      # Config da empresa + credenciais da API Fiscal (criptografadas)
        │   ├── NfeXmlService.cs           # Gera XML NF-e local (visualização/backup, sem SEFAZ)
        │   ├── ReformaTributariaService.cs# Cálculo de IBS/CBS por preset
        │   ├── FiscalPayloadBuilder.cs    # Monta o JSON de emissão (POST /emitir) para a API Fiscal
        │   ├── FiscalApiClient.cs         # HTTP client da API Fiscal (emitir/cancelar/CC-e/inutilizar/consultas)
        │   ├── FiscalApiModels.cs         # DTOs de resposta da API Fiscal + FiscalApiResult<T>
        │   ├── SecretProtector.cs         # Criptografia local (DPAPI) de API Key/CSC
        │   ├── ThermalPrinterService.cs   # Imprime o cupom não fiscal (Venda) na térmica
        │   ├── CupomPrintHelper.cs        # PrintVisual genérico p/ impressora térmica configurada
        │   ├── DanfeNfcePrintService.cs   # Imprime a DANFE simplificada da NFC-e na térmica
        │   └── QrCodeImageService.cs      # Gera o QR Code (QRCoder) da DANFE NFC-e
        ├── views/
        │   ├── LoginView.*          # Login + atualizar sistema
        │   ├── MainShellView.*      # Shell com menu lateral
        │   ├── DashboardView.*      # Vendas
        │   ├── AnalyticsView.*      # Dashboard analítico
        │   ├── EstoqueView.*
        │   ├── ClientesView.*       # Cadastro fiscal completo
        │   ├── NotaFiscalView.*     # NF-e local + integração com a API Fiscal
        │   ├── ReceiptCupomView.*       # Layout do cupom não fiscal (térmica 80mm)
        │   ├── DanfeNfceCupomView.*     # Layout da DANFE simplificada da NFC-e (térmica 80mm)
        │   ├── ConfirmPrintWindow.*     # Confirmação de impressão do cupom (Vendas)
        │   ├── CancelamentoWindow.*     # Diálogo de cancelamento de NF-e/NFC-e
        │   ├── CartaCorrecaoWindow.*    # Diálogo de CC-e
        │   ├── InutilizacaoWindow.*     # Diálogo de inutilização de numeração
        │   ├── XmlViewerWindow.*        # Visualizador de XML autorizado
        │   └── ConfiguracoesView.*  # Empresa, fiscal, API Fiscal, logo, dispositivos
        ├── models/
        ├── Database.cs
        └── FTO_App.csproj
    ```

---

## Navegação (após login)

Após autenticação, o sistema abre o **shell principal** com menu vertical à esquerda:

| Módulo | Função |
|--------|--------|
| **Vendas** | Lançamento em modal (padrão NF-e), cupom, filtros, estimativa IBS/CBS |
| **Dashboard** | Painel analítico (KPIs, top clientes) |
| **Estoque** | Produtos e categorias |
| **Clientes** | Cadastro completo (CPF/CNPJ, IE, endereço, IBGE, etc.) |
| **Nota Fiscal** | Rascunho NF-e + XML local + **emissão real via API Fiscal**, status, XML/DANFE, cancelamento, CC-e e inutilização |
| **Configurações** | Empresa, fiscal, **API Fiscal (URLs + API Key)**, logo/cupom, impressora/scanner |

O botão **Sair** retorna à tela de login.

---

## Funcionalidades

- **Controle de Acesso:** Login com usuário e senha.
- **Gestão de Vendas:** Lucro, filtros por data/cliente/status; tipo Serviço ou Venda de produto.
- **Clientes (módulo dedicado):** Cadastro fiscal com código IBGE e dados para NF-e.
- **Nota Fiscal:** Persistência de rascunhos, geração de XML local e **emissão real na SEFAZ via API Fiscal** (assinatura/transmissão/protocolo ficam a cargo da API — ver seção dedicada abaixo).
- **Configurações:** Dados da empresa, regime, ambiente NF-e, **API Fiscal (URLs + API Key)**, logo, cupom e dispositivos.
- **Estoque e Analytics:** Produtos e painel financeiro.
- **Relatórios:** Excel (.xlsx) e PDF.
- **Impressão térmica:** Cupom não fiscal e **DANFE simplificada da NFC-e** (nota fiscal do consumidor, modelo 65) já autorizada na SEFAZ.
- **Atualização automática:** Botão na tela de login.

---

## Banco de dados (PostgreSQL)

O sistema usa **PostgreSQL** (via pgAdmin, sem Docker).

1. Instale o PostgreSQL e abra o **pgAdmin**
2. Crie o banco `fto`
3. Em `FTO_Sistema/FTO_App/.env` configure **apenas** a conexão:

```env
PGHOST=localhost
PGPORT=5432
PGDATABASE=fto
PGUSER=postgres
PGPASSWORD=sua_senha
```

Na primeira abertura, o app criptografa `PGPASSWORD` com DPAPI (`enc:...`) no mesmo usuário Windows.

4. Ao abrir o app, as tabelas são criadas automaticamente
5. Em **Configurações → Banco de dados**, clique em **Migrar dados do SQLite → PostgreSQL** para importar o `FTO.db` antigo (dados preservados; o arquivo SQLite não é apagado)

A migração também preenche **CPF/CNPJ** nas vendas a partir do cadastro de clientes quando o campo estava vazio.

**Valores monetários:** o SQLite legado guarda muitos valores como texto `160.00`. Se o dashboard mostrar totais ×100 (ex.: milhões), rode de novo **Migrar SQLite → PostgreSQL** — o parser foi corrigido (ponto = decimal, não milhar).

### Produtos — tributação

No estoque, o cadastro do produto tem a aba **Tributação e impostos** (NCM, CEST, CFOP, origem, CSOSN/CST, ICMS, PIS, COFINS + **IBS/CBS**). Os dados ficam na tabela `produtos`.

### Reforma tributária — IBS / CBS

Base legal: **EC 132/2023** e **LC 214/2025** (alíquota-teste arts. 343/346/348).

| Tributo | Papel | Teste 2026 | Projetado (cheio) |
|---------|-------|------------|-------------------|
| **CBS** | Federal (substitui PIS/COFINS) | 0,9% | ~9,21% |
| **IBS** | Estados + municípios (substitui ICMS/ISS) | 0,1% (0,05% UF + 0,05% mun.) | ~18,7% |

Configurações → aba **IBS / CBS**:
- Presets: **Teste 2026**, **Projetado cheio** ou **Personalizado** (+ simulação em R$ 1.000)
- **Cálculo automático** na NF-e (checkbox no modal)
- Estimativa CBS/IBS/IVA dual no **modal de vendas** (usa alíquotas do produto quando cadastradas)
- Produto (estoque): CST, `cClassTrib`, alíquotas e % de redução
- XML local com `IBSCBS` (item: `gIBSUF`/`gIBSMun`/`vIBS`/`gCBS`) + `total.IBSCBSTot`

Em 2026 o **destaque** em DF-e é obrigatório no regime regular; o 1% é pedagógico (compensável com PIS/COFINS / dispensa de recolhimento se obrigações acessórias ok). Simples Nacional: destaque obrigatório a partir de **2027**.

### NF-e — corpo XML (autorização)

O gerador local (`NfeXmlService`) monta o corpo alinhado ao `GUIA_INTEGRACAO.md` da API Fiscal:

- **IBSCBS** no item com `gIBSUF` / `gIBSMun` / `vIBS` / `gCBS` (estrutura oficial)
- **`total.IBSCBSTot`** irmão de `ICMSTot`
- **ICMS:** `ICMS00`+CST se CRT=3; `ICMSSN`+CSOSN se Simples/MEI
- **Homologação:** `dest.xNome` fixo e `xProd` com `HOMOLOGACAO`
- UI: `idDest`, `indIEDest` (1/2/9), CSOSN, CEST, GTIN, consumidor final, presença

Ainda **não** assina nem transmite — isso fica na API (`POST /api/v1/nfe/emitir`).

Listas (Clientes, Vendas, Estoque, NF-e) usam **50 itens por página**, com a barra de paginação colada ao rodapé da grade.

---

## Configuração da empresa (banco)

Empresa, cupom, logo, CSC, ambiente, série e último número da NF-e ficam na tabela **`empresa_config`** (módulo **Configurações**). **Não** use `EMPRESA_*` / `NFE_*` / `CUPOM_*` no `.env`.

Se ainda existirem essas chaves no `.env` antigo, na primeira abertura o app importa para o banco e limpa o arquivo.

Use `.env.example` como modelo. **Nunca** faça commit do `.env`.

### Segurança

| Dado | Proteção |
|------|----------|
| `PGPASSWORD` no `.env` | DPAPI (`enc:…`) |
| CSC (Homologação/Produção) | DPAPI no PostgreSQL — pares independentes por ambiente |
| API keys (integrações) | DPAPI no PostgreSQL |
| Senhas de usuários | PBKDF2 (upgrade automático no login) |

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
3. Configure o `.env` (ou use Configurações após o login)  
4. Execute `FTO_App.exe`

---

## Integração com a API Fiscal (emissão real de NF-e/NFC-e)

O módulo **Nota Fiscal** se comunica por HTTP com microsserviços fiscais próprios (`Fiscal.NFe.API` para modelo 55 e `Fiscal.NFCe.API` para modelo 65), autenticando-se por `X-API-Key`. A API assina, transmite e consulta a SEFAZ — o FTO_App só monta o payload, chama os endpoints e mostra o resultado.

### Configuração (Configurações → Fiscal / NF-e)

| Campo | Descrição |
|---|---|
| URL base — NF-e | Endereço do `Fiscal.NFe.API` (ex.: `http://localhost:5001`) |
| URL base — NFC-e | Endereço do `Fiscal.NFCe.API` (ex.: `http://localhost:5002`) |
| API Key | Chave `pfcode_...` emitida no Portal Administrativo da API. Fica **criptografada com DPAPI** no banco (nunca em texto puro) |
| idCSC / CSC (Homologação) | Par usado quando a nota está em Ambiente = Homologação (tpAmb 2) — enviado como `X-CSC-Id`/`X-CSC-Secret` na emissão de NFC-e |
| idCSC / CSC (Produção) | Par usado quando a nota está em Ambiente = Produção (tpAmb 1). **Nunca** é o mesmo par da Homologação — gerado separadamente no portal da SEFAZ/API Fiscal |
| Testar conexão | Chama `GET /health` de cada serviço e mostra o resultado concreto (sucesso, timeout, erro de conexão) |

O par correto (Homologação × Produção) é escolhido automaticamente por `EmpresaConfig.ObterCsc(ambiente)`, com base no campo **Ambiente** de cada nota — nunca é preciso trocar manualmente ao alternar entre testes e produção.

O CNPJ da empresa (aba **Empresa**) precisa ser o **mesmo CNPJ cadastrado como Tenant** dessa API Key no Portal; caso contrário toda emissão retorna 401/403.

### Funcionalidades na tela Nota Fiscal

Dentro do formulário de cada nota, a seção **"Integração com a API Fiscal"** oferece:

| Ação | Endpoint chamado | Observação |
|---|---|---|
| 🚀 Emitir na SEFAZ | `POST /api/v1/{nfe|nfce}/emitir` | Salva a nota localmente, monta o JSON e mostra `cStat`/`xMotivo`/chave/protocolo reais da resposta |
| 📊 Consultar status | `GET /api/v1/notas/status/{chave}` | Situação normalizada (Autorizada/Cancelada/Denegada/Rejeitada/Inexistente) |
| ⬇️ Baixar XML | `GET /api/v1/notas/xml/{chave}` | Salva o `nfeProc` autorizado em disco |
| 👁️ Ver XML | idem | Abre visualizador com formatação, cópia e exportação |
| 🖨️ Baixar DANFE | `GET /api/v1/notas/danfe/{chave}` | Salva o PDF (A4) e oferece abrir na hora |
| 🧾 Imprimir na térmica (NFC-e) | *local* (sem chamada à API) | Só NFC-e (mod 65) já autorizada; ver seção **Impressão da NFC-e na térmica** abaixo |
| ✍️ Carta de Correção | `POST /api/v1/nfe/carta-correcao` | Só NF-e (mod 55); bloqueia localmente textos que tentem corrigir valor/imposto/destinatário/preço |
| 🛑 Cancelar nota | `POST /api/v1/{nfe|nfce}/cancelar` | Exige protocolo de autorização e justificativa (≥ 15 caracteres) |
| 🚫 Inutilizar numeração | `POST /api/v1/nfe/inutilizar` | Botão na barra de ferramentas (não depende de nota aberta); faixa nunca emitida |

Toda chamada retorna um `FiscalApiResult<T>` padronizado: sucesso vem com os dados tipados, falha vem com HTTP + código + mensagem prontos para exibir — nenhuma exceção da API "estoura" na tela, sempre aparece uma mensagem concreta (rede indisponível, API Key ausente, rejeição da SEFAZ, erro de validação local, etc.).

### Impressão da NFC-e na térmica

O botão **🧾 Imprimir na térmica (NFC-e)** (só habilitado para modelo 65, já com chave de acesso) gera uma **DANFE simplificada em layout 80mm** — mesmo padrão visual do cupom não fiscal (`ReceiptCupomView`/`CupomPrintHelper`) — e envia direto para a impressora térmica configurada em **Configurações → Dispositivos**, sem depender do PDF A4 devolvido pela API:

- `Views/DanfeNfceCupomView.*` monta o cupom: dados da empresa, item, totais, forma de pagamento, consumidor, número/série, chave de acesso (agrupada em blocos de 4) e protocolo de autorização.
- `Services/QrCodeImageService.cs` gera o **QR Code** localmente (biblioteca `QRCoder`) a partir da `QrCodeUrl` devolvida pela API na emissão — permite conferir a nota pelo celular sem depender de internet no app.
- `Services/DanfeNfcePrintService.cs` valida modelo/chave/impressora e reaproveita `CupomPrintHelper.ImprimirNaImpressoraConfigurada` (mesmo `PrintVisual` do cupom).
- Em **Ambiente = Homologação**, o cupom exibe um aviso "AMBIENTE DE HOMOLOGAÇÃO — SEM VALOR FISCAL".

### Contrato do payload (`FiscalPayloadBuilder`)

O JSON de emissão foi construído e conferido **campo a campo contra o código-fonte real da API** (`Fiscal.Shared.Models.NFeModels`/`ImpostoModels`/`TotalModels` e `JsonToXsdResolverService`), não apenas contra a documentação:

- `ide`, `emit`, `dest`, `det[].prod`, `det[].imposto` (ICMS/PIS/COFINS) e `total.ICMSTot` seguem a nomenclatura oficial do layout NF-e 4.00.
- Grupos "choice" do XSD (ICMS/PIS/COFINS/IBSCBS) são enviados como wrapper `*Details` (`icmsDetails`, `pisDetails`, `cofinsDetails`, `tribDetails`) com os campos **linearizados** — é assim que `JsonToXsdResolverService` da API resolve o tipo concreto (`TICMS00`, `TICMSSN102`, `TCibsNFe`, etc.), a partir do `CST`/`CSOSN` informado dentro do wrapper.
- `total.IBSCBSTot` é montado como **grupo irmão** de `ICMSTot` (nunca dentro dele — o XSD 2026 rejeita `vIBS`/`vCBS` dentro de `ICMSTot`).
- `cNF` (código numérico) e `cDV` (dígito verificador) são deixados em branco de propósito: a própria API os calcula ao montar a chave de acesso.
- Responsável Técnico (`infRespTec`) também não é enviado pelo app — a API preenche automaticamente a partir do cadastro do Tenant no Portal.

### Testando em homologação

1. No Portal Administrativo da API, cadastre o Tenant (mesmo CNPJ da empresa no FTO_App), envie o certificado A1 e gere a API Key.
2. Em **Configurações → Fiscal / NF-e**, informe as URLs dos microsserviços e a API Key; use **Testar conexão**.
3. Em **Nota Fiscal**, deixe **Ambiente = 2-Homologação** (padrão) e clique **🚀 Emitir na SEFAZ** — o destinatário e o nome do produto são automaticamente ajustados para o texto exigido em homologação.
4. Use os botões de status/XML/DANFE para conferir o resultado oficial devolvido pela SEFAZ.

> A subida da infraestrutura da API (Docker/Postgres/Portal) e o certificado digital A1 são de responsabilidade de quem opera a API Fiscal — o FTO_App já está pronto para apontar para qualquer instância (local, homologação ou produção) só trocando a URL e a API Key.

---

## Novidades recentes

- **Impressão da NFC-e na térmica:** novo botão em Nota Fiscal gera a DANFE simplificada (80mm, com QR Code) da NFC-e autorizada e envia direto para a impressora térmica — sem depender do PDF A4 da API.
- **PostgreSQL:** banco via pgAdmin; migração do SQLite (`FTO.db`) sem perda de dados.
- **Vendas:** removido o card “Resumo Geral” (espaço em branco); totais ficam no **Dashboard** analítico.
- **CPF/CNPJ nas vendas:** preenchido automaticamente a partir do cliente quando estava vazio.
- **Lista + cadastro modal:** Vendas, Clientes, NF-e e Estoque.
- **NF-e corpo autorizável:** IBSCBS/IBSCBSTot corretos, ICMSSN×CRT, homologação, idDest/indIEDest/CEST/CSOSN.
- **Integração com a API Fiscal:** emissão real, consulta de status, download de XML/DANFE, visualizador de XML, cancelamento, carta de correção e inutilização de numeração (ver seção dedicada acima).
- **Clientes:** validação CPF/CNPJ, ViaCEP (Enter), Backup/PDF.
- **Configurações → Fiscal / NF-e:** URLs e API Key da API Fiscal com teste de conexão; aba **Banco de dados**.
