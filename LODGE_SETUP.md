# Lodge System Setup

## Quick Setup (5 steps)

1. **Create LodgeManager**
   - Empty GameObject → Add `LodgeManager` component
   - Name it "LodgeManager"

2. **Create your lodge prefab**
   - Import/build your lodge 3D model
   - Drag it into scene temporarily
   - Drag it to `Assets/Prefabs/` folder to create prefab
   - Delete from scene

3. **Add LodgeBuilder tool**
   - Find/create your tools GameObject
   - Add `LodgeBuilder` component
   - In inspector:
     - **Lodge Prefab**: Drag your prefab here
     - **Mountain Manager**: Drag your MountainManager
     - **Lift Builder**: Drag your LiftBuilder
     - **Simulation Runner**: Drag your SimulationRunner
     - Camera auto-detects if left empty

4. **Add to BuildActionBar**
   - Select BuildActionBar GameObject
   - Find "Facilities" tab → Tools list
   - Add element → Drag LodgeBuilder component into it

5. **Done!**
   - Click Facilities → Lodge button
   - Preview follows mouse (green = valid, red = invalid)
   - Left-click to place ($25,000)
   - Right-click or ESC to cancel

## Settings you can adjust

**LodgeBuilder:**
- `Tree Clear Radius`: How far to clear trees (default: 15m)
- `Build Cost`: Cost to place (default: $25,000)

**LodgeFacility** (added automatically, but you can adjust on placed lodges):
- `Capacity`: Max skiers (default: 10)
- `Rest Duration Seconds`: How long skiers stay inside (default: 30 seconds)

**SkierVisualizer:**
- `Lodge Check Radius`: How far skiers look for lodges (default: 30m)
- `Lodge Visit Chance`: Probability skiers visit after trail (default: 0.15 = 15%)

## That's it!

The system handles everything else automatically:
- Trees clear on placement
- Snap zones created for trail connections
- Skiers visit lodges, rest, and respawn
- Capacity management works automatically
