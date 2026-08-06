# Looga Tags

Looga Tags adds color-coded project tags to GameObjects. Use the GameObject inspector header to add the tag component, then select one or more tags from the project database.

The tag database is stored at `Assets/Resources/LoogaSoft/LoogaTagDatabase.asset`. This keeps project data outside the package so package updates cannot replace authored tags.

## Runtime API

Runtime code uses the `LoogaSoft.Tags.Runtime` namespace. The primary types are `LoogaTags`, `LoogaTagGroup`, `LoogaTag`, and `LoogaTagDatabase`.

Unity migration metadata preserves serialized tag components and databases from earlier Toolkit versions. The editor also moves the former database asset to the Looga Tags resource path when it first loads the project.
