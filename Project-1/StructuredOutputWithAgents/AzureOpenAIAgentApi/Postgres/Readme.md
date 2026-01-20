docker build -t postgres-employee-db .
docker run -d -p 5432:5432 --name postgres-container postgres-employee-db
docker exec -it postgres-container psql -U postgres -d yourdatabase
SELECT * FROM employee;



