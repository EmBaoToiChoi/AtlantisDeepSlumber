BEFORE YOU START:
- you need Unity 6 +
- you need URP SRP pipeline 17 if you use higher please import a higher support pack.
- wind setup is in wind prefab at each scene, its useful for smoke particles

Step 1 
Go to "PC_Renderer" or version of that file that you use:
1. You can improve the FPS amount by if you change the rendering path from forward to deferred at the rendering setting. The scene has many reflection probes.
Change the Rendering path from forward to deferred. Forward render is ok too but it's slower for big open scenes.
2. Add decal support if you want to use our decals on walls:
  Add Renderer Feature --> Decal:
--> Max Draw distance 200-300; 
--> !!If you use deferred render change decal technique to avoid flickering seams "AUTO" doesn't work well (bugged) on deferred!! "Use SCREEN SPACE" for deferred render.
---> at initial unity 6 versions deferred render is bugged at all, shadows on terrain moves with camera position, terrain render not properly. 
Probably for some time forward+ is best option but heavy.
Step 2         
        - Import the VFX graph particles "Visual Effect Graph” if you want to use distortion particles on fire and lava, you can find it in the package manager, its unity system.
        - Turn on few options if they are turned off in "PC_Renderer", or other profile that you use:
	       * "Opaque Texture" this will fix distortion particles - they need grab the screen color (depth buffer)
	       * "Depth Texture" this will fix distortion particles - they need grab the screen color (depth buffer)
	- Optionaly use 1k or 2k shadow resolution. We used 2k.
	- Turn on HDR if it is turned off
        
Step 3 Go to project settings: 
    - Player and set:  Color Space to Linear
    - Quality settings: Go to quality settings and: 
	 * turn off vsync
	 * lod bias should be around 1 you ofc could use higher but it will generate more triangles
                        
Step 4 Find "Demo Castle Scene" and open it.

Step 5 - HIT PLAY!:)

Step 6 -  Make note that Unity often compiles shaders even after you hit play for a long time, so performance will rise after Unity ends the shader compilation
Wait a moment until it ends. 

About scene construction:
		- There is a post-process profile: Manage post process by scene post-process object.
		- Prefab wind manages wind speed and direction at the scene

IMPORTANT!
We suggest to rebake light at scene with resolution 9 (atm its 7 or lower to reduce pack size at asset store)
