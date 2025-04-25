# LOMM Unity Data Extractor
A unity project compatible with Unity 6.x and URP.
This provides a menu under Tools -> Generate All Assets
Will run for a couple of minutes and create new assets in your current Unity project.
Everything is under the GeneratedAssets folder.

## **Overview**
* Reads LOMM files - DAT, ABC, DTX, and SPR
* Converts all DTX files into PNG
* Converts all variations of textures into Materials (some DTXs have different levels of transparency set via DAT files)
* Converts all ABC files into meshes and prefabs
* May convert an ABC as multiple prefabs - one for each set of textures that references the file
* Converts DAT files into a prefab with child prefab meshes and objects
* Each DAT prefab will contain correctly placed, sized ABC models (props and more)

## **TODO**
* Transparency fixes
* Cleanup Skybox generation
* Optimize pipeline
* Better grouping of BSP objects and WorldObjects
* Add scripts to some objects to track original DAT properties
* Create Lights (port code from parent project)
* Create Sounds (port code from parent project)
* Create Scene or better prefab with entire "map" in one place
* Possibly import gibs
* Possibly add skeletons and animations to models





