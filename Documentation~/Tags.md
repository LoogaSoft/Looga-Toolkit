# Looga Tags

Looga Tags adds color-coded project tags to GameObjects. Use the `Components` and `Tags` tabs in the GameObject inspector utility strip to switch between component filters and tag authoring. The compact control at the end of the tab row collapses both areas when inspector space is limited.

Selecting the first tag adds the hidden runtime component. Clearing the final tag removes that component, so untagged objects have no runtime tag behavior. The standard `Clear` button appears immediately before the add button and is disabled when no tags are selected.

The tag database is stored at `Assets/Resources/LoogaSoft/LoogaTagDatabase.asset`. This keeps project data outside the package so package updates cannot replace authored tags.

## Runtime API

Runtime code uses the `LoogaSoft.Tags.Runtime` namespace. The primary types are `LoogaTags`, `LoogaTagGroup`, `LoogaTag`, and `LoogaTagDatabase`.

Unity migration metadata preserves serialized tag components and databases from earlier Toolkit versions. The editor also moves the former database asset to the Looga Tags resource path when it first loads the project.
