# AuthFlow AppKit

Intégration réutilisable AuthFlow (activation de licence + vérification des
mises à jour intégrée à l'app) pour les applications ASP.NET Core Blazor
Server (Kestrel + Windows Service), extraite de la vraie intégration pilote
validée sur `Brackford-0brien/FneManuelInvoiceApp` (`dev/web-version`). Deux
packages NuGet, publiés sur le feed GitHub Packages de ce repo :

| Package | Contenu |
|---|---|
| `AuthFlow.AppKit` | Librairie de classes : store/manager/hosted-service de licence, `AppUpdateService`, et les composants Razor UI (`AuthFlowBanner`, `LicenseActivation`, `UpdateCheck`). |
| `AuthFlow.Template` | Un template pack `dotnet new` qui génère une nouvelle app Blazor Server déjà câblée avec `AuthFlow.AppKit` + le SDK AuthFlow, l'hébergement Kestrel/Windows Service, l'installeur Inno Setup, et le pipeline CI/CD de release générique. |

Il dépend de `AuthFlow.LicenseClient` (le SDK AuthFlow, depuis
`Brackford-0brien/AuthFlow`, `sdk/dotnet/AuthFlow.LicenseClient`). Ce SDK
n'est pas compilé depuis les sources dans ce repo - le `.nupkg` pré-construit
est vendored sous `local-nuget-feed/` et republié sur ce même feed GitHub
Packages par `publish-package.yml`, donc les consommateurs n'ont jamais
besoin de la copie vendored ni du repo source `AuthFlow` - juste du feed de
ce repo.

## Configurer le feed GitHub Packages sur votre poste dev

Les feeds NuGet GitHub Packages nécessitent une authentification même en
lecture seule sur des feeds publics appartenant à un compte utilisateur
(c'est une limitation de GitHub, pas quelque chose de spécifique à ce repo).
Il vous faut un **Personal Access Token (PAT)** avec **exactement le scope
`read:packages`** (PAT classique) - rien d'autre n'est nécessaire pour
*consommer* les packages. (Un PAT fine-grained avec "Packages: Read"
fonctionne aussi, mais un PAT classique + `read:packages` est plus simple.)

**Étape par étape :**

1. Allez sur https://github.com/settings/tokens → "Generate new token (classic)".
2. Cochez uniquement `read:packages`. Donnez-lui une expiration qui vous convient.
3. Générez-le et copiez la valeur du token (commence par `ghp_...`).
4. Créez un `nuget.config` **à côté de votre `.sln`/projet** (ou globalement
   dans `%AppData%\NuGet\NuGet.Config` pour le rendre disponible à tous les
   projets de votre machine) avec :

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="authflow-appkit-github" value="https://nuget.pkg.github.com/Brackford-0brien/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <authflow-appkit-github>
      <add key="Username" value="YOUR_GITHUB_USERNAME" />
      <add key="ClearTextPassword" value="ghp_YOUR_read_packages_TOKEN" />
    </authflow-appkit-github>
  </packageSourceCredentials>
</configuration>
```

Ou de façon équivalente via la CLI (écrit la même chose) :

```powershell
dotnet nuget add source https://nuget.pkg.github.com/Brackford-0brien/index.json `
  -n authflow-appkit-github -u YOUR_GITHUB_USERNAME -p ghp_YOUR_read_packages_TOKEN --store-password-in-clear-text
```

C'est le **seul** setup local nécessaire. Pas de clone du repo, pas de feed
local, aucune étape supplémentaire - `dotnet new install` / `dotnet restore`
font le reste.

> **Ne committez jamais un vrai token dans `nuget.config`.** Gardez-le plutôt
> dans votre `%AppData%\NuGet\NuGet.Config` au niveau utilisateur si vous
> craignez de le committer par accident, ou ajoutez `nuget.config` au
> `.gitignore` du projet que vous créez.

## Créer une nouvelle app en 2 commandes

```powershell
dotnet new install AuthFlow.Template::*
dotnet new authflow-blazor -n MyNewApp
cd MyNewApp
dotnet build
```

La valeur de `-n` devient le nom du projet/namespace/service, le GUID `AppId`
de l'installeur est régénéré automatiquement à chaque instanciation. Ajustez
les chaînes `ProductDisplayName`/`ServiceBaseName`/branding directement dans
`Program.cs`/`appsettings.json` après génération si elles doivent différer
du nom du projet.

**Le `.csproj` généré référence déjà `AuthFlow.AppKit` - aucune étape
manuelle `dotnet add package` n'est nécessaire.** `AuthFlow.LicenseClient`
(le SDK) est aussi résolu automatiquement, en tant que dépendance transitive
de `AuthFlow.AppKit` (vous ne le verrez pas comme référence *top-level* dans
`dotnet list package` - cette commande ne liste que les références directes
par défaut - mais `dotnet restore` / `dotnet build` le récupèrent sans rien
faire de plus). Vérifié pour de vrai avec `dotnet list package` sur un
projet fraîchement généré :

```
Top-level Package                                Requested    Resolved
> AuthFlow.AppKit                                 0.1.0        0.1.0
> Microsoft.Extensions.Hosting.WindowsServices    10.0.0       10.0.0
> Radzen.Blazor                                   6.0.0        6.0.0
```

Ça génère un projet Blazor Server avec :
- `Program.cs` pré-câblé avec `builder.Services.AddAuthFlowAppKit(...)`, `builder.Host.UseWindowsService(...)`, binding Kestrel depuis la config.
- `appsettings.json` avec une section `AuthFlow` prête pour le `ProductId`/`ServerUrl`/`PublicKeys` de votre produit.
- `installer/setup.iss` (Inno Setup) avec un GUID `AppId` fraîchement généré et le nom de votre app/service substitué.
- `.github/workflows/release.yml` - le même pipeline CI/CD générique et paramétrable validé sur FneManuelInvoiceApp (build → Inno Setup → checksum → GitHub Release sur un repo public de releases séparé). Configurez juste `RELEASES_REPO` / `RELEASES_REPO_TOKEN` pour le repo de releases de la nouvelle app (voir `INSTALLER.md` §7 de FneManuelInvoiceApp pour les étapes exactes - non dupliquées ici pour éviter la divergence ; copiez ce doc à côté du workflow quand vous générez une nouvelle app).

## Publier une nouvelle version du package/template

1. Bumpez `<Version>` dans `src/AuthFlow.AppKit/AuthFlow.AppKit.csproj` (et
   `template/AuthFlow.Template.csproj` si vous avez modifié le template).
2. `git tag vX.Y.Z && git push origin vX.Y.Z`
3. `.github/workflows/publish-package.yml` build, pack, et pousse
   `AuthFlow.AppKit`, `AuthFlow.Template`, **et** le nupkg vendored du SDK
   `AuthFlow.LicenseClient` vers
   `https://nuget.pkg.github.com/Brackford-0brien/index.json`, le tout avec
   le `GITHUB_TOKEN` par défaut (les permissions Actions de ce repo sont
   réglées sur `packages: write`, ce qui est tout ce dont GitHub Packages a
   besoin pour un package appartenant au même repo/owner - aucun PAT
   supplémentaire requis pour publier).
4. Vous pouvez aussi déclencher une publication sans tag via **Actions →
   Publish AuthFlow.AppKit NuGet package → Run workflow** (`workflow_dispatch`)
   - utile pour republier uniquement le nupkg vendored du SDK s'il change
   sans bumper la version de AppKit.

## Vérifier qu'un poste dev propre peut vraiment consommer les packages

Ça a été testé pour de vrai, pas juste supposé :
`.github/workflows/test-clean-consume.yml` (à la demande,
`workflow_dispatch`) tourne sur un runner GitHub tout neuf - littéralement
rien de préinstallé à part le SDK .NET - et fait exactement ce que ferait un
nouveau membre de l'équipe :

1. Écrit un `nuget.config` avec seulement `nuget.org` + le feed GitHub
   Packages de ce repo, authentifié avec un PAT stocké dans le secret
   `TEST_READ_PACKAGES_PAT` (scope : `read:packages` uniquement).
2. `dotnet new install AuthFlow.Template::0.1.0`
3. `dotnet new authflow-blazor -n TestApp`
4. `dotnet restore` + `dotnet list package` dans le projet généré
5. `dotnet build -c Release`

Résultat du dernier run réel : **tout vert**, `AuthFlow.AppKit 0.1.0` résolu
automatiquement comme package top-level (voir la sortie exacte dans la
section "Créer une nouvelle app" ci-dessus), `AuthFlow.LicenseClient` résolu
en transitif, et l'app générée a buildé avec 0 warning/0 erreur - confirmé
sans feed local, sans clone du repo, sans `dotnet add package` manuel, rien
au-delà du `nuget.config` documenté + un PAT en lecture seule.

Si vous voulez relancer cette vérification vous-même (par ex. après un
changement de package), ajoutez un secret de repo `TEST_READ_PACKAGES_PAT`
(PAT classique, scope `read:packages`) une fois, puis lancez le workflow
depuis l'onglet Actions quand vous voulez re-vérifier.

## Ce que contient `AuthFlow.AppKit`

- `Services/AuthFlowLicenseFileStore.cs` - persiste le token `.lic` importé sous `App_Data/authflow-license.lic`.
- `Services/AuthFlowLicenseManager.cs` - logique partagée d'activation+vérification (utilisée au démarrage et par la page d'import).
- `Services/AuthFlowLicenseHostedService.cs` + `AuthFlowLicenseConfig` - exécute la vérification initiale + périodique de licence en tant qu'`IHostedService`. Le service de heartbeat propre au SDK est enregistré séparément par `AddAuthFlowLicense` (depuis `AuthFlow.LicenseClient`) et démarré automatiquement par le même host - aucun câblage supplémentaire nécessaire.
- `Services/AuthFlowLicenseState.cs` - snapshot du dernier résultat de vérification à l'échelle du processus + notification de changement, lu par la bannière/pages Razor.
- `Services/AppUpdateService.cs` - encapsule l'`IUpdateChecker` du SDK, ajoute le téléchargement + la vérification de checksum SHA-256 + le lancement de l'installeur, avec un fallback optionnel de téléchargement authentifié via l'API GitHub pour les repos de releases privés (pas nécessaire avec le pattern recommandé de repo de releases public).
- `Components/AuthFlowBanner.razor` - bannière d'avertissement non bloquante (licence invalide/hors-ligne), à intégrer dans votre `MainLayout`.
- `Components/LicenseActivation.razor` - page d'upload/import de fichier `.lic` (`/license-activation`).
- `Components/UpdateCheck.razor` - page "vérifier les mises à jour" (`/update-check`), avec bouton téléchargement+installation.
- `AuthFlowAppKitOptions.cs` - `ProductDisplayName` / `CompanyName` / `ServiceBaseName`, les leviers utilisés pour dé-coder en dur les chaînes spécifiques à l'app (noms de dossiers temporaires, nom de fichier installeur de secours, User-Agent, titres UI/pied de page) qui étaient auparavant codées en dur à "FneManuelInvoiceApp" dans le code pilote d'origine.
- `ServiceCollectionExtensions.cs` - `services.AddAuthFlowAppKit(configuration, options => {...})`, l'appel unique qui câble tout ce qui précède.

### Utilisation dans `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthFlowAppKit(builder.Configuration, options =>
{
    options.ProductDisplayName = "My New App";
    options.CompanyName = "Brackford-0brien";
    options.ServiceBaseName = "MyNewApp"; // used for temp folder / installer filename fallback
});

builder.Host.UseWindowsService(options =>
{
    options.ServiceName = builder.Configuration["WindowsService:ServiceName"] ?? "MyNewApp";
});
```

### Structure `appsettings.json` attendue

```json
{
  "AuthFlow": {
    "ProductId": "your-product-id",
    "ServerUrl": "https://auth-flow-dun.vercel.app",
    "LicenseKey": "", // dev/testing fallback only - production uses the .lic import flow
    "GracePeriodDays": 7,
    "HeartbeatIntervalMinutes": 60,
    "HttpTimeoutSeconds": 20,
    "PublicKeys": {
      "test-key": "MCowBQYDK2VwAyEA..."
    }
  },
  "GitHub": {
    "Token": "" // only needed if your installer releases repo is PRIVATE - not recommended, see below
  }
}
```

## Test réel de bout en bout effectué

Les deux packages ont été construits, packagés et validés pour de vrai (pas
juste compilés) :

1. `dotnet pack` sur `AuthFlow.AppKit.csproj` et `template/AuthFlow.Template.csproj` → les deux ont produit des `.nupkg` valides.
2. `dotnet new install AuthFlow.Template.0.1.0.nupkg` depuis un feed local → le template s'est enregistré avec succès (`authflow-blazor`).
3. `dotnet new authflow-blazor -n TestApp` → génère un projet avec `TestApp.csproj`, un GUID `AppId` d'installeur fraîchement généré substitué dans `setup.iss`, et toutes les références `AuthFlowApp1` renommées en `TestApp`.
4. `dotnet build` sur le projet généré → **0 warning, 0 erreur**.
5. `dotnet run` avec un vrai `ProductId`/`LicenseKey`/`PublicKeys` du produit AuthFlow pilote de FneManuelInvoiceApp, contre le vrai serveur de production `https://auth-flow-dun.vercel.app` :
   - `POST /api/activate` et `POST /api/validate` ont été appelés pour de vrai (visible dans les logs), confirmant que les `AuthFlowLicenseManager`/`AuthFlowLicenseHostedService` extraits fonctionnent à l'identique une fois séparés de l'app d'origine.
   - La licence (désormais révoquée, suite aux tests précédents du pilote) est bien revenue en `Revoked` - prouvant que le pipeline de validation de signature Ed25519 du SDK fonctionne toujours de bout en bout à travers le code extrait.
   - `GET /api/updates/check` a été appelé pour de vrai depuis la page `/update-check` de l'app générée, confirmant que `AppUpdateService` fonctionne aussi.
   - `/`, `/license-activation`, et `/update-check` ont tous retourné HTTP 200 avec les titres de page attendus une fois `AdditionalAssemblies`/`AddAdditionalAssemblies` câblés dans `Routes.razor`/`Program.cs` pour qu'ASP.NET Core découvre les composants routés par `@page` vivant dans l'assembly `AuthFlow.AppKit` référencé (un bug d'intégration réel et non évident, détecté uniquement par ce run réel, pas par `dotnet build` seul).
6. Les artefacts de test (`dotnet new install` temporaire, dossier `TestApp` généré) ont été nettoyés ensuite.

## Notes de conception / hypothèses reprises du pilote

- **Hypothèse de déploiement mono-tenant** : une instance serveur/processus
  par client, donc le store de licence est un simple fichier plat, pas une
  ligne de base de données par tenant.
- **Dépendance à Radzen.Blazor** : les composants Razor extraits utilisent
  Radzen (`RadzenIcon`, `RadzenButton`, `RadzenAlert`, etc.) - c'est une
  dépendance de package explicite de `AuthFlow.AppKit`. Confirmé avec le
  client : la plupart de ses apps utilisent déjà Radzen, donc ça reste une
  dépendance dure (pas rendue optionnelle) - gardé simple volontairement.
- **Pattern de repo de releases public recommandé** : ne pas distribuer de
  token GitHub aux machines clientes juste pour télécharger les installeurs.
  Créez un second repo public, sans code, par app (ex. `MyNewApp-releases`)
  et pointez la variable `RELEASES_REPO` du workflow de release dessus - les
  téléchargements deviennent alors entièrement anonymes. Le chemin
  authentifié via l'API GitHub d'`AppUpdateService` n'est qu'un fallback
  défensif, pour le cas (déconseillé) d'un repo de releases privé.
