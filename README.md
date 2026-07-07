# AuthFlow AppKit

Intégration réutilisable AuthFlow (activation de licence + vérification des
mises à jour intégrée à l'app) pour les applications ASP.NET Core Blazor
Server (Kestrel + Windows Service), extraite de la vraie intégration pilote
validée sur `Brackford-0brien/FneManuelInvoiceApp` (`dev/web-version`). Trois
packages NuGet, **publiés publiquement sur nuget.org** :

| Package | Contenu |
|---|---|
| [`AuthFlow.LicenseClient`](https://www.nuget.org/packages/AuthFlow.LicenseClient) | Le SDK AuthFlow (validation de licence Ed25519 hybride online/offline, heartbeat, update-check). Source : `Brackford-0brien/AuthFlow`, `sdk/dotnet/AuthFlow.LicenseClient`. |
| [`AuthFlow.AppKit`](https://www.nuget.org/packages/AuthFlow.AppKit) | Librairie de classes : store/manager/hosted-service de licence, `AppUpdateService`, et les composants Razor UI (`AuthFlowBanner`, `LicenseActivation`, `UpdateCheck`). Dépend de `AuthFlow.LicenseClient`. |
| [`AuthFlow.Template`](https://www.nuget.org/packages/AuthFlow.Template) | Un template pack `dotnet new` (`PackageType=Template`) qui génère une nouvelle app Blazor Server déjà câblée avec `AuthFlow.AppKit` + le SDK AuthFlow, l'hébergement Kestrel/Windows Service, l'installeur Inno Setup, et le pipeline CI/CD de release générique. Aussi visible dans le sélecteur **Créer un nouveau projet** de Visual Studio (recherchez "authflow") une fois l'indexation nuget.org terminée. |

`AuthFlow.LicenseClient` n'est pas compilé depuis les sources dans ce repo -
le `.nupkg` pré-construit est vendored sous `local-nuget-feed/` (copié depuis
une build locale du repo source `AuthFlow`) et republié par ce repo vers
nuget.org à chaque nouvelle version. Les consommateurs n'ont jamais besoin de
la copie vendored ni du repo source `AuthFlow` - juste de nuget.org.

## Aucune configuration de feed nécessaire

Les 3 packages sont publics sur **nuget.org**, la source NuGet par défaut sur
n'importe quel poste avec le SDK .NET installé. **Aucun `nuget.config`,
aucun PAT, aucune authentification n'est nécessaire** pour les consommer -
ni pour `dotnet restore`/`dotnet build`, ni pour `dotnet new install`.

> Historique : ces packages étaient initialement publiés sur le feed GitHub
> Packages (privé) de ce repo, ce qui nécessitait un PAT `read:packages` côté
> consommateur. Ils ont depuis été republiés sur nuget.org (public) pour que
> le template `dotnet new` soit utilisable sans aucune étape d'authentification
> et découvrable depuis Visual Studio. `AuthFlow.LicenseClient`/`AuthFlow.AppKit`
> restent des bibliothèques génériques sans donnée client réelle en dur (voir
> "Sécurité" plus bas) - c'est ce qui rend leur publication publique acceptable.

## Créer une nouvelle app en 2 commandes (template v0.1.2)

Le template `AuthFlow.Template` a été publié en v0.1.2 pour ajouter le tag
langage C# et améliorer la découverte Visual Studio. Pour créer une nouvelle
app :

```powershell
dotnet new install AuthFlow.Template
dotnet new authflow-blazor -n MonApp
cd MonApp
dotnet build
```

Note : aucune version explicite n'est nécessaire (la commande installe la
dernière version stable du template). Si vous préférez fixer une version
précise : `dotnet new install AuthFlow.Template::0.1.2`.

### Exemple de nuget.config (optionnel)

Si vous travaillez en interne et préférez configurer un `nuget.config` local
(ex. pour basculer de nuget.org vers un miroir), voici un exemple minimal :

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

### PAT GitHub (scope minimal `read:packages`) — uniquement si vous utilisez
un feed GitHub Packages privé

1. Allez sur https://github.com/settings/tokens → "Generate new token (classic)".
2. Cochez uniquement `read:packages`.
3. Copiez la valeur et placez-la dans `nuget.config` (clé `ClearTextPassword`) ou
   configurez-la avec la CLI :

```powershell
dotnet nuget add source https://nuget.pkg.github.com/Brackford-0brien/index.json `
  -n authflow-appkit-github -u YOUR_GITHUB_USERNAME -p ghp_YOUR_read_packages_TOKEN --store-password-in-clear-text
```

(Attention : ne commitez jamais un PAT dans le repo.)

### Packages runtime publics (nuget.org)

- https://www.nuget.org/packages/AuthFlow.LicenseClient (SDK de licence)
- https://www.nuget.org/packages/AuthFlow.AppKit (librairie AppKit)
- https://www.nuget.org/packages/AuthFlow.Template (dotnet-new template)

Le template référence automatiquement `AuthFlow.AppKit` ; `AuthFlow.LicenseClient`
est résolu transitivement.

### Dépannage rapide

- Si `dotnet run` renvoie `ERR_ADDRESS_INVALID` ou l'app ne démarre pas sur
  le port attendu : essayez `dotnet run --urls http://localhost:5000` ou
  vérifiez la variable d'environnement `ASPNETCORE_URLS` / la section `Kestrel`
  du `appsettings.json`.
- Si `dotnet restore` échoue à résoudre `AuthFlow.AppKit`, vérifiez que la
  résolution nuget pointe bien vers `nuget.org` (aucun PAT n'est nécessaire
  pour les packages publics). Si vous voyez une erreur de version, essayez
  `dotnet nuget locals all --clear` pour vider le cache local, puis
  `dotnet restore`.

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
> AuthFlow.AppKit                                 0.1.1        0.1.1
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
   `template/AuthFlow.Template.csproj` si vous avez modifié le template, et
   `template/content/AuthFlowApp1/AuthFlowApp1.csproj` pour pointer la bonne
   version d'`AuthFlow.AppKit` dans le projet généré). Si le SDK
   `AuthFlow.LicenseClient` a changé, bumpez aussi sa version dans
   `Brackford-0brien/AuthFlow` et copiez le nouveau `.nupkg` dans
   `local-nuget-feed/`.
2. `dotnet pack` chaque projet modifié en local pour vérifier que ça
   compile et se pack sans erreur avant de publier.
3. Publiez sur nuget.org, **dans cet ordre de dépendance** (LicenseClient
   d'abord, sinon la restauration d'AppKit échoue le temps que
   LicenseClient soit indexé) :

   ```powershell
   dotnet nuget push local-nuget-feed\AuthFlow.LicenseClient.<version>.nupkg -s https://api.nuget.org/v3/index.json -k <NUGET_ORG_API_KEY>
   dotnet nuget push local-nuget-feed\AuthFlow.AppKit.<version>.nupkg        -s https://api.nuget.org/v3/index.json -k <NUGET_ORG_API_KEY>
   dotnet nuget push local-nuget-feed\AuthFlow.Template.<version>.nupkg     -s https://api.nuget.org/v3/index.json -k <NUGET_ORG_API_KEY>
   ```

   L'API key se génère sur https://www.nuget.org/account/apikeys (scope
   "Push", limité aux `PackageId` `AuthFlow.*`). **nuget.org n'autorise pas
   de republier un numéro de version déjà poussé** - un bump de `<Version>`
   est donc obligatoire à chaque publication, même pour un simple correctif.
4. `git tag vX.Y.Z && git push origin vX.Y.Z` pour garder une trace de la
   version publiée dans l'historique git.
5. Comptez quelques minutes pour l'indexation nuget.org avant qu'une
   nouvelle version soit résolvable par `dotnet restore`, et un peu plus
   longtemps avant que `AuthFlow.Template` apparaisse/se mette à jour dans
   le sélecteur **Créer un nouveau projet** de Visual Studio (cache côté VS,
   pas seulement l'indexation nuget.org).

## Vérifier qu'un poste dev propre peut vraiment consommer les packages

Ça a été testé pour de vrai, pas juste supposé - deux fois :

1. **Historique (feed GitHub Packages)** : `.github/workflows/test-clean-consume.yml`
   a fait tourner un runner GitHub tout neuf qui installe le template et
   build le projet généré avec un PAT `read:packages` en lecture seule.
2. **Actuel (nuget.org public)** : depuis un dossier local totalement propre
   (aucun cache NuGet réutilisé, `nuget.config` limité à `nuget.org` +
   éventuellement un feed de staging pour tester une version pas encore
   indexée), `dotnet new install AuthFlow.Template`, puis
   `dotnet new authflow-blazor -n TestApp`, puis `dotnet restore` et
   `dotnet build -c Release` - **0 warning, 0 erreur, aucune source NuGet ni
   aucun token GitHub configuré**. C'est exactement le scénario visé par la
   demande client "aucune dépendance GitHub pour un dev qui crée un new
   projet" : nuget.org seul suffit.


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

## Sécurité : pourquoi publier ces packages publiquement sur nuget.org est acceptable

Ces 3 packages sont volontairement génériques et ne contiennent **aucune**
donnée propre à un client ou à un produit réel :

- Aucun `ProductId`, `licenseKey`, ou clé publique de signature réels codés
  en dur - `appsettings.json` du template a ces champs vides/à remplir par
  le développeur qui génère l'app.
- La seule URL en dur est `https://auth-flow-dun.vercel.app`, l'endpoint
  **public** de la plateforme SaaS AuthFlow elle-même (pas un secret) -
  c'est l'équivalent de documenter l'URL d'une API publique.
- La logique de vérification de signature Ed25519/validation de licence est
  un algorithme générique (basé sur des clés publiques fournies en config) -
  la sécurité du système ne repose jamais sur le secret du code du SDK, mais
  sur la clé **privée** de signature qui ne quitte jamais le serveur AuthFlow.

Ce qui **reste privé** (ne va jamais sur nuget.org) : le code source du
serveur AuthFlow lui-même (`Brackford-0brien/AuthFlow`, la partie plateforme/
API, pas le dossier SDK), les vraies licences/clients/`ProductId` de chaque
client, et le code source métier des apps clientes (FneManuelInvoiceApp,
RadissonConnect, etc.) qui *consomment* ces packages.
