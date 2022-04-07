local paths = {
    "/",
    "/api/cheesegull/s/1",
    "/api/cheesegull/s/1?raw",
    
    "/api/cheesegull/b/75",
    "/api/cheesegull/b/75",
    
    "/api/cheesegull/search",
    "/api/cheesegull/search?q=peppy",
    "/api/cheesegull/search?a=50",
    "/api/cheesegull/search?o=2",
    "/api/cheesegull/search?p=1",
    "/api/cheesegull/search?m=1",
    "/api/cheesegull/search?r=1",
    
    "/api/cheesegull/search?raw",
    "/api/cheesegull/search?q=peppy?raw",
    "/api/cheesegull/search?a=50?raw",
    "/api/cheesegull/search?o=2?raw",
    "/api/cheesegull/search?p=1?raw",
    "/api/cheesegull/search?m=1?raw",
    "/api/cheesegull/search?r=1?raw",
    
    "/api/cheesegull/f/Kenji Ninuma - DISCOPRINCE (peppy) [Normal].osu", -- Super slow...
        
    "/api/v1/hash/a2c959cccc8ff7fc54d3b8c6beb41af2",
    
    "/osu/a2c959cccc8ff7fc54d3b8c6beb41af2",
    "/osu/75",
    
    "/d/1",
    "/d/1n",
    
    "/d/1?ipfs=true",
    "/d/1n?ipfs=true"
}

math.randomseed(os.time())

randomPath = function()
  local path = math.random(1,table.getn(paths))
  return paths[path]
end

request = function()
  path = randomPath()
  return wrk.format(nil, path)
end


