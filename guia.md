# Guia de atualização automática — FTO Sistema

Este guia explica como publicar uma nova versão e como o botão **Atualizar sistema** (tela de login) funciona no computador do cliente.

---

## Visão geral

```
Você (dev)                         GitHub                              Cliente
-----------                        ------                              -------
1. Altera código                   3. Action gera publish              
2. Push + cria Release (tag)  -->  4. Anexa FTO_App-win-x64.zip  -->  5. Clica "Atualizar sistema"
                                                                       6. Baixa ZIP, troca arquivos,
                                                                          preserva .env e FTO.db
```

Repositório usado pela API: `kapimkk/FTO-Main`  
Asset esperado na Release: `FTO_App-win-x64.zip`

---

## Como lançar uma nova versão (desenvolvedor)

### 1. Commit e push

```powershell
git add .
git commit -m "Descrição das alterações"
git push origin main
```

### 2. Criar a Release no GitHub

1. Abra: https://github.com/kapimkk/FTO-Main/releases/new  
2. Em **Choose a tag**, crie uma tag no formato `v1.1.0` (sempre `v` + número maior que a versão anterior)  
3. Título: ex. `FTO Sistema 1.1.0`  
4. Descreva as mudanças (aparece no app se houver update)  
5. Clique em **Publish release**

> Não precisa anexar o ZIP manualmente. O GitHub Actions faz isso.

### 3. Aguardar o workflow

1. Vá em **Actions** → workflow **Build Release Package**  
2. Espere ficar verde  
3. Volte na Release: o arquivo `FTO_App-win-x64.zip` deve estar nos assets

### 4. Testar no app

1. Abra o FTO na máquina do cliente (ou a sua instalação atual)  
2. Na tela de login, clique em **Atualizar sistema**  
3. Confirme → o app baixa, fecha, atualiza e reabre  

---

## Versionamento

| Onde | O que fazer |
|------|-------------|
| Tag da Release | `v1.0.0`, `v1.1.0`, `v2.0.0`… |
| Comparação no app | Tag remota vs versão do executável |
| Build na Action | A versão do `.exe` é definida pela tag automaticamente |

Regra: a tag nova **precisa ser maior** que a versão instalada.  
Ex.: instalado `v1.0.0` → próxima release `v1.0.1` ou `v1.1.0`.

Opcional (alinha o código local): atualize também em `FTO_Sistema/FTO_App/FTO_App.csproj`:

```xml
<Version>1.1.0</Version>
<InformationalVersion>1.1.0</InformationalVersion>
```

---

## O que o botão faz (cliente)

1. Consulta `https://api.github.com/repos/kapimkk/FTO-Main/releases/latest`  
2. Compara a versão da tag com a versão local  
3. Se houver update, pergunta se deseja instalar  
4. Baixa `FTO_App-win-x64.zip`  
5. Extrai em pasta temporária  
6. Dispara um script PowerShell que:  
   - espera o `FTO_App.exe` fechar  
   - copia os arquivos novos  
   - **não sobrescreve** `.env`, `FTO.db`, `FTO.db-shm`, `FTO.db-wal`  
   - reabre o sistema  

---

## Repositório público vs privado

### Público (recomendado para distribuição)

Nenhuma configuração extra. O app baixa a release sem token.

### Privado

1. Crie um **Personal Access Token** (classic) com leitura do repositório/releases  
2. No `.env` do cliente (pasta do executável), adicione:

```env
FTO_UPDATE_TOKEN=ghp_seu_token_aqui
```

O modelo está em `.env.example`.

---

## Disparo manual do build (opcional)

Se a Release já existe mas o ZIP falhou:

1. **Actions** → **Build Release Package** → **Run workflow**  
2. Informe a tag (ex. `v1.1.0`)  
3. Execute  

O ZIP será reenviado com `--clobber` (substitui o anterior).

---

## Primeira instalação (sem update)

Para quem ainda não tem o sistema:

1. Baixe `FTO_App-win-x64.zip` da Release mais recente  
2. Extraia em uma pasta (ex. `C:\FTO Sistema`)  
3. Configure o `.env`  
4. Execute `FTO_App.exe`  

Depois disso, updates futuros podem ser feitos só pelo botão na tela de login.

---

## Solução de problemas

| Problema | O que verificar |
|----------|-----------------|
| “Nenhuma release encontrada” | Ainda não existe Release publicada no GitHub |
| “sem o arquivo FTO_App-win-x64.zip” | Workflow não rodou ou falhou — veja **Actions** |
| “Você já está na versão mais recente” | Tag da Release não é maior que a versão instalada |
| Erro 401 / 404 em repo privado | `FTO_UPDATE_TOKEN` ausente ou sem permissão |
| App não reabre | Abra manualmente o `FTO_App.exe`; veja se o PowerShell não foi bloqueado |
| Perdeu dados | Não deveria — confirme que `.env` e `FTO.db` estão na mesma pasta do `.exe` |

---

## Arquivos relacionados

```
FTO_Sistema/FTO_App/
  Services/UpdateService.cs      → lógica de verificação/download
  views/LoginView.xaml(.cs)      → botão "Atualizar sistema"
  FTO_App.csproj                 → versão local de desenvolvimento

.github/workflows/release.yml    → build + upload do ZIP na Release
guia.md                          → este documento
```

---

## Checklist rápido de release

- [ ] Código commitado e no `main`  
- [ ] Tag nova maior que a versão atual (`vX.Y.Z`)  
- [ ] Release publicada no GitHub  
- [ ] Action **Build Release Package** verde  
- [ ] Asset `FTO_App-win-x64.zip` visível na Release  
- [ ] Teste do botão **Atualizar sistema** na tela de login  
