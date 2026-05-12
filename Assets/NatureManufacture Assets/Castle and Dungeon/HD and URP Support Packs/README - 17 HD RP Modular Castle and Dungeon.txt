BEFORE YOU START:
- you need Unity 6 or higher 
- you need HD SRP pipeline 17 if you use higher etc custom shaders could not work but seems they should. 
That's why we provide 16 version which seems to work with much higher versions as well. 
For all higher RP versions please use 17 HD RP support pack.

Step 1 
 	 !!!! IMPORTANT !!!! Open "Project settings" ->"Gaphics"-> "Diffusion Profile List"
	and drag and drop our SSS settings diffusion profiles for particles, glass, obsidian into Diffusion profile list:
		  NM_SSSSettings_Glass
		  NM_SSSSettings_Obsidian
		  NM_SSSSettings_Smoke
	Without this glass, obsidian and smoke materials will not become affected by scattering and they will look wrong.
        There is also a fix button on each material that uses these profiles and it will automatically add profiles to the list from the material panel.

Step 2 Open "Project settings" and "Quality" and set:
	- Set VSync to don't sync

Step 3 Find the "Demo Castle" scene and open it.
        - Choose way of movement. Movie track or free movement.
	- Choose camera and turn on or off "playable director" and "animation" or leave free camera movement turned on.

Step 4 - HIT PLAY!:)

Step 5 -  Make note that Unity often compiles shaders even after you hit play for a long time, so performance will rise after Unity ends the shader compilation
Wait a moment until it ends. 
to misunderstanding. 

About scene construction:
		- Prefab wind manages wind speed and direction at the scene
		 You could adjust the fog resolution, we set it to low as it's the most expensive thing at the scene. For better devices, you could use medium quality.
		- Remember to have "always refresh" at the scene window turned on, it's in "toggle skybox, fog, and various other effects".
 You can find it at the top right at the scene window. Without this option turned off fog and wind will not refresh properly at scene view, and will work only at playmode.

IMPORTANT1
We suggest to rebake light at scene with resolution 9 (atm its 7-5 or lower to reduce pack size at asset store)

IMPORTANT2:
If you notice in the console error:
No more space in Reflection Probe Atlas. To solve this issue, increase the size of the Reflection Probe Atlas in the HDRP settings.
UnityEngine.GUIUtility:ProcessEvent (int,intptr,bool&)
Just change reflection atlas size at hd rp settings into 4kx8k or higher.
Go to: Edit--> Project Settings --> find "Reflection 2D Atlas Size" --> set it to 8x8k or lower if it would not throw any error on 4x8k for example.


IMPORTANT3:
If you notice in the console warning:
Maximum reflection probes on screen reached. To fix this error, increase the 'Maximum Cube Reflection Probes on Screen' property in the HDRP asset.
UnityEngine.GUIUtility:ProcessEvent (int,intptr,bool&)
Go to: Edit--> Project Settings --> find "Maximum Cube Reflection Probes on Screen" --> set it to 64

IMPORTANT4
At initial Unity 6 version we found issue with DDX and DDY nodes at HD RP which we use in our shading.
They get special unity function to switch them off in RTX reflection images but they still throw error (if API in player settings is not automatic).
They work as they should, no issues in render or anything (tested at RTX render) but unity have hardcoded to throw error. We already send message to unity team to fix that.
We suggest to use automatic API in player settings. RTX works fine so ignore errors. 
It may happend unity with this error will not be able to bake lightmaps (engine memory leaks), so just turn on automatic API for lightbake.
Info if from and for initial unity 6 release so for sure alot will be fixed by unity team.


