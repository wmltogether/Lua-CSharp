-- testing debug library
local a =1

local function multi_assert(expected,...)
    local arg = {...}
    for i = 1, #arg do
        assert(arg[i]==expected[i])
    end
end
local function test_locals(x,...)
    local b ="local b"
    assert(debug.getlocal(test_locals,1) == "x")
    multi_assert({"x",1},debug.getlocal(1,1))
    multi_assert({"b","local b"},debug.getlocal(1,2))
    multi_assert({"(vararg)",2},debug.getlocal(1,-1))
    multi_assert({"(vararg)",3},debug.getlocal(1,-2))
    multi_assert({"a",1},debug.getlocal(2,1))
    assert(debug.setlocal(2,1,"new a") == "a")

end

test_locals(1,2,3)
assert(a == "new a")

local function test_upvalues()
    local a =3
    local function f(x)
        local b = a + x
        local function g(y)
            local c = b + y
            local function h()
                return a+b+c
            end
            multi_assert({"a",3},debug.getupvalue(h,1))
            multi_assert({"b",4},debug.getupvalue(h,2))
            multi_assert({"c",6},debug.getupvalue(h,3))
            multi_assert({"b",4},debug.getupvalue(g,1))
            multi_assert({"a",3},debug.getupvalue(g,2))
            multi_assert({"a",3},debug.getupvalue(f,1))
            debug.setupvalue(h,1,10)
            debug.setupvalue(h,2,20)
            debug.setupvalue(h,3,30)
            assert(h() == 60)
        end
        g(2)
    end
    f(1)
end
test_upvalues()
local mt = {
    __metatable = "my own metatable",
    __index = function (o, k)
        return o+k
    end
}
debug.setmetatable(10, mt)
assert(debug.getmetatable(10) == mt)
a = 10
assert( a[3] == 13)

assert(debug.traceback(print)==print)
assert(debug.traceback(print)==print)



assert(type(debug.getregistry())=="table")