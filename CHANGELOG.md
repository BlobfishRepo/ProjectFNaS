# Changelog
## April 4
### Added
- Mold backend first-pass implementation added. The Mold will spread from sources, hindering movement, camera visibility, and light visibility to relevant regions.
- Added a new waypoint node close at the left-side of the hallway.
- Added a Mold Spray (set to E) to revert status of a patch of mold.

### Issues
- Significant issues with framerate are present.

## April 2
### Added
- There is now subtle view bobbing when the player moves.

### Changed
- Movement now paused during Mimic jumpscare.

### Fixed
- Fixed a bug where moving between doors rapidly led to odd camera behavior.
- Made it harder for the door to close on the player while moving between door-waypoints.

## April 1
### Added
- Mimic added. The mimic spawns at various locations around the apartment, and needs to be shined with the flashlight or else the player will be jumpscared. Jumpscares from the mimic only take battery away, instead of a full game-over.
- Settings now incorporate the mimic.

### Fixed
- Stalker now properly looks toward the Player/Cameras.

## March 30
### Added
- Added a "Reset to Default" button to restore default settings.
- Slider rows have a value input field.

### Changed
- Settings now works on a JSON save/load system.
- Settings can be loaded from either the Settings or Gameplay scenes.
- Settings UI made data-driven to simplify adding new settings.

## March 29
### Added
- Added jumpscare logic and jumpscare animation to Stalker.
- Updated Stalker model (still WIP).

### Note
- Settings are undergoing an overhaul, so they are unlikely to work properly.

## March 20
### Added
- Lost Girl added. She can spawn in glass-based objects (TV, windows), and after progressing a certain number of stages lunges at the player.
- Added flashlight stun mechanic to both Stalker and Lost Girl.

### Changed
- Backend: Stalker and Lost Girl movement systems unified.

### Fixed
- Stalker could jumpscare after being reset using the flashlight if the player tries to walk past too soon.

## March 8
### Added
- Functionality added to settings menu, and additional settings relating to Stalker behavior and flashlight duration added
- Added a temporary model for the paper

### Changed
- Pressing "Y" to reset now leads to the Settings page

## March 7
### Added
- Booting up the game now brings up an intro Settings page; note functionality will be added in the next update

### Changed
- Backend: Stalker and Player entities completely refactored, so movement should now be separate from other script

## March 6
### Added
- New views added to Bathroom and the Other Bedroom

### Changed
- Global lighting intensity slightly increased
- Flashlight shape made much more narrow
- WASD movement now hold-to-move through waypoints

### Fixed
- Transition into bathroom now properly lerps instead of snapping

## March 3
### Changed
- Doors hold-to-open logic refined slightly

### Fixed
- Flashlight works through doors again


## March 2
### Changed
- Flashlight now tied to cursor, and using the flashlight on the Stalker requires correct cursor placement
- Door decoupled from movement system and temporarily set to hold-to-open (although extremely buggy)

### Removed
- Flashlight and door logic tied to the movement system removed

### Fixed
- Fixed bug where Stalker can move forward while Player was moving, resulting in unintended behavior

## March 1 - Final MVP Build
### Added
- Added a percentage that goes up when looking at the desk, serving as the win condition
- Added win/loss conditions, with a reset button
- Added a mesh to the Stalker entity
- Added opening/closing doors

### Changed
- Camera UI modified (buttons slightly changed, added a minimap indicator)
- Camera lighting made slightly brighter
- Stalker AI modified to fit needs of MVP
- Backend: movement nodes reworked to allow for interacting with doors, and general clean-up
- Backend: classes renamed to better fit current purpose

## February 28
### Added
- Lighting overhauled to try to match a dark atmosphere
- Flashlight has light
- MVP Apartment (2 bedrooms, part of hallway, bathroom layout)

### Changed
- Nodes and Views matched to the apartment

### Fixed
- Stalker now properly gets stalled by camera/player, and pushed back by flashlight

## February 27
### Added
- Stalker basic AI
- Flashlight, activated using F (literally no visual indication though)

### Changed
- Node system overhauled to keep track of node, player, and camera positions

## February 25
### Added
- Movement compass indicator in bottom-left corner showing possible directions

### Changed
- Edge view icons changed to solid lines


## February 23
### Added
- "View" system allowing for looking in different places within the same node, affecting WASD movement
- GUI connected to View system

### Changed
- Camera now fits aspect ratio
- Movement system transitions made smoother


## February 22
### Added
- Camera System (Live feed, camera screen)

### Changed
- Movement Nodes modified to allow for rotations per node (but it's very janky right now)

### Removed
- Limited Camera Movement


## February 21

### Added
- Movement Nodes + Node Prefab

### Issues
- Main camera movement VERY buggy right now, will fix later



## February 15

### Added

* First-person camera
* Limited camera movement
