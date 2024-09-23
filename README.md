# Shopping List

This is a Caves of Qud mod that adds a customizable shopping list. For the full description, see the [Steam Workshop page](https://steamcommunity.com/sharedfiles/filedetails/?id=3092205971).

Shopping List is licensed under the [GNU General Public License v3](http://www.gnu.org/licenses/agpl.html), which can be found in full in [LICENSE.md](LICENSE.md).

## Changelog

### 23 September, 2024
#### Version 1.2
* Re-pathed all instances of `Ava` to `Ceres`.
* Refactored mod parts for compatibility -- `Ceres_ShoppingList_Highlighter` is now an `IScribedPart`, while `Ceres_ShoppingList_ShoppingListPart` is now an `IPlayerPart` that is manually scribed.
* Removed a temporary workaround that was used to prevent a Linux-exclusive bug where the shopping list part would be repeatedly duplicated. This bug was fixed in game version 206.75.

### 11 July, 2024
#### Version 1.1
* Fixed a harmless error that would occur when entering an invalid object ID.
* Added a setting that allows control over the colors of highlighted NPCs. It defaults to magenta, but can also be changed between red, orange, yellow, green, blue, cyan, and white.

### 22 June, 2024
#### Version 1.0.5
* Updated to work with game version 2.0.207.72, which featured breaking API changes.

### 6 June, 2024
#### Version 1.0.4
* Updated to work with the Spring Molting patch (2.0.207.63).

### 27 April, 2024

#### Version 1.0.3
* Updated to work with game version 2.0.206.77, which featured breaking API changes.

### 7 April, 2024

#### Version 1.0.2
* Potential workaround for a Linux-exclusive bug in the vanilla code that caused shopping lists to double up indefinitely whenever the player switched bodies, such as through domination. Thanks to Krast and Duct Vader on Steam for reporting and helping diagnose this, and thank you *very* much to kernelmethod and librarianmage from the Caves of Qud Discord for discovering the workaround and spending several hours helping me try various solutions.

### 6 February, 2024

#### Version 1.0.1
* Removed some extraneous code that's no longer needed due to vanilla API changes.
* Display names now reference the item itself, not its pluralization (i.e. the shopping list will say "lead slug" rather than "lead slugs".) This isn't an ideal solution, but it seems to be the only way to prevent name weirdness with books and the like. Future changes might include a patch around for this, if I can find one.

### 22 November, 2023
* Initial release.