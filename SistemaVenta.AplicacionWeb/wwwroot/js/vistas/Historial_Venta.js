const VISTA_BUSQUEDA = {
    busquedaFecha: () => {
        $("#txtFechaInicio").val("")
        $("#txtFechaFin").val("")
        $("#txtNumeroVenta").val("")

        $(".busqueda-fecha").show()
        $(".busqueda-venta").hide()
    },
    busquedaVenta: () => {
        $("#txtFechaInicio").val("")
        $("#txtFechaFin").val("")
        $("#txtNumeroVenta").val("")

        $(".busqueda-fecha").hide()
        $(".busqueda-venta").show()
    }
}

$(document).ready(function () {
    VISTA_BUSQUEDA["busquedaFecha"]()

    $.datepicker.setDefaults($.datepicker.regional["es"])

    $("#txtFechaInicio").datepicker({ dateFormat: "dd/mm/yy" })
    $("#txtFechaFin").datepicker({ dateFormat: "dd/mm/yy" })
})

$("#cboBuscarPor").change(function () {
    if ($("#cboBuscarPor").val() == "fecha") {
        VISTA_BUSQUEDA["busquedaFecha"]()
    } else {
        VISTA_BUSQUEDA["busquedaVenta"]()
    }
})

$("#btnBuscar").click(function () {
    if ($("#cboBuscarPor").val() == "fecha") {
        if ($("#txtFechaInicio").val().trim() == "" || $("#txtFechaFin").val().trim() == "") {
            toastr.warning("", "Debe Ingresar fecha inicio y fin")
            return;
        }
    } else {
        if ($("#txtNumeroVenta").val().trim() == "") {
            toastr.warning("", "Debe Ingresar el número de venta")
            return;
        }
    }

    let numeroVenta = $("#txtNumeroVenta").val()
    let fechaInicio = $("#txtFechaInicio").val()
    let fechaFin = $("#txtFechaFin").val()

    $(".card-body").find("div.row").LoadingOverlay("show");

    fetch(`/Venta/Historial?numeroVenta=${numeroVenta}&fechaInicio=${fechaInicio}&fechaFin=${fechaFin}`)
        .then(response => {
            $(".card-body").find("div.row").LoadingOverlay("hide");
            return response.ok ? response.json() : Promise.reject(response);
        })
        .then(responseJson => {

            $("#tbventa tbody").html("");

            if (responseJson.length > 0) {
                responseJson.forEach((venta) => {

                    //  LÓGICA CORREGIDA: Determinar si puede facturar
                    const esTicket = venta.idTipoDocumentoVenta === 1;
                    const tieneUUID = venta.uuid && venta.uuid.trim() !== "";
                    const puedeFacturar = esTicket && !tieneUUID;

                    // Crear la fila base
                    let $fila = $("<tr>").append(
                        $("<td>").text(venta.fechaRegistro),
                        $("<td>").text(venta.numeroVenta),
                        $("<td>").text(venta.tipoDocumentoVenta),
                        $("<td>").text(venta.nombreCliente),
                        $("<td>").text(venta.total)
                    );

                    // Crear columna de acciones
                    let $colAcciones = $("<td>");

                    //  Botón Ver Detalles - SIEMPRE visible
                    $colAcciones.append(
                        $("<button>")
                            .addClass("btn btn-info btn-sm mr-1")
                            .append($("<i>").addClass("fas fa-eye"))
                            .data("venta", venta)
                            .attr("title", "Ver Detalles")
                    );

                    //  Botón Solicitar Factura - SOLO si puede facturar
                    if (puedeFacturar) {
                        $colAcciones.append(
                            $("<button>")
                                .addClass("btn btn-success btn-sm btn-solicitar-factura")
                                .append($("<i>").addClass("fas fa-file-invoice"))
                                .data("venta", venta)
                                .attr("title", "Solicitar Factura")
                        );
                    }

                    // Agregar columna de acciones a la fila
                    $fila.append($colAcciones);

                    // Agregar fila a la tabla
                    $("#tbventa tbody").append($fila);
                })
            }
        })
})

//  Botón para ver detalles
$("#tbventa tbody").on("click", ".btn-info", function () {
    let d = $(this).data("venta")

    $("#txtFechaRegistro").val(d.fechaRegistro)
    $("#txtNumVenta").val(d.numeroVenta)
    $("#txtUsuarioRegistro").val(d.usuario)
    $("#txtTipoDocumento").val(d.tipoDocumentoVenta)
    $("#txtNombreCliente").val(d.nombreCliente)
    $("#txtSubTotal").val(d.subTotal)
    $("#txtIGV").val(d.impuestoTotal)
    $("#txtTotal").val(d.total)

    $("#tbProductos tbody").html("");

    d.detalleVenta.forEach((item) => {
        $("#tbProductos tbody").append(
            $("<tr>").append(
                $("<td>").text(item.descripcionProducto),
                $("<td>").text(item.cantidad),
                $("<td>").text(item.precio),
                $("<td>").text(item.total),
            )
        )
    })

    $("#linkImprimir").attr("href", `/Venta/MostrarPDFVenta?numeroVenta=${d.numeroVenta}`)
    $("#modalData").modal("show");
})

//  Botón Solicitar Factura - CORREGIDO: SOLO UNA APERTURA
$("#tbventa tbody").on("click", ".btn-solicitar-factura", function () {
    let venta = $(this).data("venta");
    let $boton = $(this);
    let $fila = $boton.closest('tr');

    swal({
        title: "¿Solicitar Factura?",
        text: `¿Desea solicitar factura para el ticket ${venta.numeroVenta}?`,
        type: "warning",
        showCancelButton: true,
        confirmButtonText: "Sí, Solicitar",
        cancelButtonText: "Cancelar",
        closeOnConfirm: false,
        closeOnCancel: true
    }, function (isConfirm) {
        if (isConfirm) {
            // Mostrar loading
            swal({
                title: "Procesando...",
                text: "Generando factura electrónica. Por favor espere.",
                type: "info",
                showConfirmButton: false,
                allowOutsideClick: false
            });

            //  Llamada al endpoint de timbrado
            fetch('/Venta/SolicitarFactura', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    numeroVenta: venta.numeroVenta,
                    idVenta: venta.idVenta
                })
            })
                .then(response => response.json())
                .then(data => {
                    if (data.estado) {
                        //  CERRAR LOADING
                        swal.close();

                        //  ABRIR PDF AUTOMÁTICAMENTE - UNA SOLA VEZ
                        const pdfUrl = `/Venta/DescargarFacturaPDF?numeroVenta=${venta.numeroVenta}`;

                        // Abrir en nueva ventana
                        const pdfWindow = window.open(pdfUrl, '_blank');

                        // Si se bloqueó el popup, mostrar alerta
                        if (!pdfWindow || pdfWindow.closed || typeof pdfWindow.closed === 'undefined') {
                            toastr.warning("Por favor, habilite los popups para ver el PDF automáticamente.");
                        }

                        //  DESCARGAR XML automáticamente
                        setTimeout(function () {
                            const linkXML = document.createElement('a');
                            linkXML.href = `/Venta/DescargarFacturaXML?numeroVenta=${venta.numeroVenta}`;
                            linkXML.download = `Factura_${venta.numeroVenta}.xml`;
                            linkXML.style.display = 'none';
                            document.body.appendChild(linkXML);
                            linkXML.click();
                            document.body.removeChild(linkXML);
                        }, 500);

                        //  OCULTAR botón completamente
                        $boton.remove();

                        //  ACTUALIZAR columna "Tipo Documento"
                        $fila.find('td:eq(2)').text('Factura');

                        //  MOSTRAR VENTANA EMERGENTE DE ÉXITO
                        setTimeout(function () {
                            swal({
                                title: "¡Factura Generada!",
                                text: `Factura ${venta.numeroVenta} generada exitosamente.\n\nUUID: ${data.objeto.uuid}\n\nEl PDF se ha abierto en una nueva ventana y el XML se ha descargado.`,
                                type: "success",
                                confirmButtonText: "Entendido"
                            });
                        }, 600);

                        //  NOTIFICACIÓN TOASTR
                        setTimeout(function () {
                            toastr.success("PDF abierto y XML descargado", "Factura timbrada correctamente");
                        }, 800);

                    } else {
                        //  ERROR
                        swal({
                            title: "Error",
                            text: data.mensaje || "No se pudo generar la factura",
                            type: "error",
                            confirmButtonText: "Cerrar"
                        });
                    }
                })
                .catch(error => {
                    console.error("Error:", error);
                    swal({
                        title: "Error",
                        text: "Error al comunicarse con el servidor. Intente nuevamente.",
                        type: "error",
                        confirmButtonText: "Cerrar"
                    });
                });
        }
    });
});

