extends Camera

func _ready():
    #Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
    pass

var sens = 0.22
var mouse_delta = Vector2()
func _input(event):
    if event is InputEventMouseMotion:
        mouse_delta += event.relative


var walkspeed = 8.0

func _process(_delta):
    sens = 0.022 * 2.5
    if Input.is_action_just_pressed("esc"):
        if Input.mouse_mode != Input.MOUSE_MODE_CAPTURED:
            Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
        else:
            Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
    walkspeed = 8.0
    if Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
        rotation_degrees.y -= mouse_delta.x*sens
        rotation_degrees.x -= mouse_delta.y*sens
        rotation_degrees.x = clamp(rotation_degrees.x, -89, 89)
    
    mouse_delta *= 0.0
