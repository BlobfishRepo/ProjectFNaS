# Changelog
## March 6
### Added
- New views added to Bathroom and the Other Bedroom

### Changes
- Global lighting intensity slightly increased
- Flashlight shape made much more narrow
- WASD movement now hold-to-move through waypoints

### Fixed
- Transition into bathroom now properly lerps instead of snapping

## March 3
### Changes
- Doors hold-to-open logic refined slightly

### Fixed
- Flashlight works through doors again


## March 2
### Changes
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
