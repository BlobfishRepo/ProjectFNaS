# Changelog
# April 14
### Added
- Certain views now support moving camera freely within a bounding box.

### Changed
- Improved performance of writing on paper.
- Updated apartment minimap on camera to match new apartment layout.
- Updated button stylings on camera minimap.
- Moved Move Compass to bottom-right corner and made unavailable movement options brighter.

# April 13
### Added
- Added a Jumpscare animation to the Mimic.

### Changed
- Improved performance of Mimic model.
- Camera now only renders when in view of the player.
- Made Mimic sound based on relative location.

### Issues
- Paper is laggy again.

## April 12
### Added
- Added Nights 1-5 with different presents, and with save/load for the most recent night.
- Can use Escape to return to the intro menu from gameplay.
- Added backpack model to Mimic and a separate jumpscare model while animations are WIP.

### Changed
- Added paper settings to settings page.
- Settings page no longer includes every possible setting (all settings can still be edited in the JSON file if desired).
- Lost Girl and Mold have some audio cues.

### Removed
- Removed paper percentage display in top-right corner.

### Fixed
- Fixed issues with mold spray, flashlight, and door interact working after winning/losing the game.
- Fixed door interact working even if you move away from the location.
- FPS drops related to the paper writing should now be stable.

### Issues
- Possible performance issues with Mimic.

## April 11
### Added
- Mold spray now has particle effects.
- Paper now supports custom text, and has a writing animation that fits text. Writing on paper fills out the paper progressively.

### Changed
- Paper now requires using R to write and progress the percentage.
  - Can write on either the Monitor or Paper views.
- Writing on paper now has audio.
- Added a small loading screen to preload writing assets.

### Fixed
- Fixed mold patches being able to spread to locations that were currently being sprayed by the player.

### Issues
- Writing on paper had framerate issues, which may or may not have been resolved.

## April 10
### Added
- 3 new cameras added monitoring:
  - Pantry
  - Living room
  - Dining room
- Added Navmesh pathfinding to Lost Girl.

### Changed
- Movement nodes and views expanded to allow movement through:
  - Hallway
  - Living room
  - TV area
  - Dining room
  - Kitchen
- Security monitor updated to account for new cameras.
- Adjusted position of some cameras, mold patches, mimic spawns, and Lost Girl glasses to account for new layout.
- Adjusted light in several rooms.
- Lost Girl AI changed:
  - Chase state now using navmesh pathfinding.
  - Stalling/pushback now based on LOS or viewing on camera, not flashlight.

### Fixed
- Increased radius of mold spray to make it easier to target mold clumps.

## April 9
### Added
- Apartment model now has added rooms:
  - Pantry
  - Hallway
  - Living room
  - Kitchen

## April 7
### Changed
- Added Camera-only lights, improving visibility.

### Fixed
- Fixed a bug with the movement compass not displaying the correct possible moves in an override situation.
- Fixed empty overrides not properly preventing movement.

## April 5
### Added
- Mold AI added to settings page.
- Blood mode for Mold properly added.

### Changed
- Apartment lighting changed to mixed lighting for significantly improved framerate.
- Monitor static noise changed to a shader to reduce load on CPU.
- Slight tweaks to lighting and global volume settings.

### Issues
- Debug settings go off the page.
- Security Cameras are basically unreadable.

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
