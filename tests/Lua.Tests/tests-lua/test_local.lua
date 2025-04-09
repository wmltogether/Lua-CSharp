sun = {}
sun.mass = 1

local bodies = { sun, sun, sun, sun, sun }

local function test_local(b, len)
    for i = 1, len do
        local bi = b[i]
        local bim = bi.mass
        print(bi.mass)
    end
end

local len = #bodies

test_local(bodies, len)


print"OK"