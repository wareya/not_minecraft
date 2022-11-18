extends QuakelikeBody

var accel = 16.0
var walkspeed = 8.0
var wishdir = Vector3()
var velocity = Vector3()
var jumpspeed = 10.0
var gravity = -9.8


var inputs = {}
var old_inputs = {}

var sprite = Vector2(0, 2)

var time = 0.0

func radians_difference(angle1, angle2):
    var diff = angle2 - angle1
    return diff if abs(diff) < PI else diff + (PI*2*-sign(diff))

func radians_move_toward(angle1, angle2, amount):
    var diff = radians_difference(angle1, angle2)
    amount = abs(amount) * sign(diff)
    if abs(amount) > abs(diff):
        return angle2
    else:
        return fmod(fmod(angle1+amount+PI, PI*2)+PI*2, PI*2)-PI

func degrees_move_toward(angle1, angle2, amount):
    return rad2deg(radians_move_toward(deg2rad(angle1), deg2rad(angle2), deg2rad(amount)))

func do_ai(delta, world, player):
    
    if is_on_wall() and (oldpos-global_translation).length()/delta < 0.5:
        inputs["jump"] = true
    
    var p_diff : Vector3 = global_translation - player.global_translation
    var p_dist : float = p_diff.length()
    
    if p_dist < 5.0:
        var p_away : Vector3 = p_diff
        p_away.y = 0
        p_away = -p_away.normalized()
        var p_angle = Vector2(p_away.z, p_away.x).angle()
        
        $AngleHolder.global_rotation.y = radians_move_toward($AngleHolder.global_rotation.y, p_angle, deg2rad(360)*delta)
        inputs["up"] = true

onready var oldpos = global_translation
func _process(delta):
    sprite = Vector2(4, 4)
    walkspeed = 3.0
    
    time += delta
    var world : GameWorld = get_tree().get_nodes_in_group("World")[0]
    var player : QuakelikeBody = get_tree().get_nodes_in_group("Player")[0]
    
    old_inputs = inputs
    
    inputs["up"] = false
    inputs["down"] = false
    inputs["left"] = false
    inputs["right"] = false
    inputs["jump"] = false
    if old_inputs.size() == 0:
        old_inputs = inputs
    
    do_ai(delta, world, player)
    
    wishdir = Vector3()
    if inputs["up"]:
        wishdir.z -= 1
    if inputs["down"]:
        wishdir.z += 1
    if inputs["left"]:
        wishdir.x -= 1
    if inputs["right"]:
        wishdir.x += 1
    
    if wishdir.length_squared() > 1.0:
        wishdir = wishdir.normalized()
    
    var local_wishdir = wishdir.rotated(Vector3.UP, $AngleHolder.global_rotation.y)
    
    var newvel = velocity.move_toward(local_wishdir*walkspeed, delta*walkspeed*accel)
    velocity.x = newvel.x
    velocity.z = newvel.z
    
    gravity = -32.0
    jumpspeed = 8.4
    if (inputs["jump"] and is_on_floor()) or (inputs["jump"] and !old_inputs["jump"]):
        velocity.y = jumpspeed
        detach_from_floor()
    
    velocity.y += gravity/2.0*delta
    velocity.y = max(velocity.y, -78.4)
    
    var _oldvel = velocity
    oldpos = global_translation
    velocity = custom_move_and_slide(delta, velocity)
    
    world.oopsie_body(self)
    
    velocity.y += gravity/2.0*delta
    velocity.y = max(velocity.y, -78.4)
    
    $Sprite3D.flip_h = false
    $Sprite3D.frame_coords = sprite * Vector2(1, 2)
    
    var cam = get_viewport().get_camera()
    var rel_pos = Vector3(0, 0, 1).rotated(Vector3.UP, $AngleHolder.global_rotation.y - cam.global_rotation.y)
    
    if abs(rel_pos.x) > abs(rel_pos.z):
        $Sprite3D.frame += 2
        if fmod(time, 0.8) < 0.4:
            $Sprite3D.frame += 1
        if rel_pos.x > 0:
            $Sprite3D.flip_h = true
    elif rel_pos.z > 0:
        $Sprite3D.frame += 1
    
    $Sprite3DLower.flip_h = $Sprite3D.flip_h
    $Sprite3DLower.frame_coords = $Sprite3D.frame_coords + Vector2(0, 1)
    
    if abs(rel_pos.x) <= abs(rel_pos.z) and fmod(time, 0.8) < 0.4:
        $Sprite3DLower.flip_h = !$Sprite3DLower.flip_h
