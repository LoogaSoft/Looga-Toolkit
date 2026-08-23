# Package Updates

Open **LoogaSoft > Package Support** and select **Updates**.

The Updates page finds direct `com.loogasoft.*` dependencies in the project manifest. It compares each installed revision with its Git repository.
The **Package Support** menu command shows a checkmark when updates are waiting.

## Statuses

- **Current**: The installed revision matches the repository.
- **Update available**: A newer release tag is available.
- **Unreleased changes**: The default branch contains newer source without a newer release tag.
- **Local development**: The project uses a local package source. Looga does not replace local source.
- **Unavailable**: Git or the remote repository could not provide update data.

Use **Update** for a tagged release. Use **Install Source** only when the project must use unreleased source.

**Update All** installs Looga Toolkit last. The update queue continues after an assembly reload.

Looga stores update data in the project `Library` folder. The cache expires after 24 hours. Use **Check Now** to refresh it immediately.

## Releases

Use semantic versions in each package `package.json` file. Create a matching Git tag, such as `v1.1.0`, when the package is ready for other projects.
