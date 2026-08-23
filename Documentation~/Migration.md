# Migrating To Looga Toolkit

Looga Toolkit replaces these packages:

- `com.loogasoft.loogainspector`
- `com.loogasoft.loogahierarchy`
- `com.loogasoft.loogatools`
- `com.loogasoft.polytags`
- `com.loogasoft.loogaprefabbrowser`
- `com.loogasoft.loogalogger`

Remove those dependencies from `Packages/manifest.json`. Add this dependency:

```json
"com.loogasoft.loogatoolkit": "https://github.com/LoogaSoft/Looga-Toolkit.git"
```

Packages that depend on a replaced package must depend on Looga Toolkit instead. Update tag assembly references to `LoogaSoft.Tags.Runtime` or `LoogaSoft.Tags.Editor`, and update source imports to `LoogaSoft.Tags.Runtime` or `LoogaSoft.Tags.Editor`.

Serialized tag components and databases migrate automatically because Toolkit preserves their Unity asset GUIDs and declares their former serialized type names.

Logger components and code do not require a type migration. Toolkit preserves the `LoogaSoft.Logger.*` assembly identities, the `LoogaSoft.Logging` namespaces, and the logger asset GUIDs. Remove the standalone Logger package only after installing a Toolkit release that contains the Logging module.

Do not install Toolkit together with any package that it replaces. The duplicate assembly identities will prevent Unity from compiling the project.
